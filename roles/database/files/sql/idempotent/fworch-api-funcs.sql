

CREATE OR REPLACE FUNCTION public.get_visible_devices_per_tenant(integer)
    RETURNS SETOF device_type 
    LANGUAGE 'plpgsql'
    STABLE 
AS $BODY$
DECLARE
	i_tenant_id ALIAS FOR $1;
	i_dev_id integer;
    v_dev_name VARCHAR;
	b_can_view_all_devices boolean;
BEGIN
    SELECT INTO b_can_view_all_devices tenant_can_view_all_devices FROM tenant WHERE tenant_id=i_tenant_id;
    IF b_can_view_all_devices THEN
        FOR i_dev_id, v_dev_name IN SELECT dev_id, dev_name FROM device
        LOOP
            RETURN NEXT ROW (i_dev_id, v_dev_name);
        END LOOP;
    ELSE
        FOR i_dev_id, v_dev_name IN SELECT device_id, dev_name FROM tenant JOIN tenant_to_device USING (tenant_id) LEFT JOIN device ON (tenant_to_device.device_id=device.dev_id) WHERE tenant.tenant_id=i_tenant_id
        LOOP
            RETURN NEXT ROW (i_dev_id, v_dev_name);
        END LOOP;
    END IF;
    RETURN;
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.get_visible_managements_per_tenant(integer)
    RETURNS SETOF device_type 
    LANGUAGE 'plpgsql'
    STABLE 
AS $BODY$
DECLARE
	i_tenant_id ALIAS FOR $1;
	i_mgm_id integer;
    v_mgm_name VARCHAR;
	b_can_view_all_devices boolean;
    i_dev_id integer;
BEGIN
    SELECT INTO b_can_view_all_devices tenant_can_view_all_devices FROM tenant WHERE tenant_id=i_tenant_id;
    IF b_can_view_all_devices THEN
        FOR i_mgm_id, v_mgm_name IN SELECT mgm_id, mgm_name FROM management
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    ELSE
        -- return all managements belonging to devices the tenant can view - derive it from get_visible_devices_per_tenant:
        FOR i_mgm_id, v_mgm_name IN SELECT DISTINCT mgm_id, mgm_name FROM management WHERE mgm_id IN (SELECT mgm_id FROM device WHERE dev_id IN (SELECT id FROM get_visible_devices_per_tenant(i_tenant_id)))
        LOOP
            RETURN NEXT ROW (i_mgm_id, v_mgm_name);
        END LOOP;
    END IF;
	RETURN;
END;
$BODY$;

CREATE OR REPLACE FUNCTION public.filter_rule_nwobj_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF object
 LANGUAGE sql
 STABLE
AS $function$
  SELECT o.*
  FROM rule_nwobj_resolved r JOIN object o ON (r.obj_id=o.obj_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed >= import_id)
  GROUP BY o.obj_id
  ORDER BY MAX(obj_name), o.obj_id
$function$;

CREATE OR REPLACE FUNCTION public.filter_rule_svc_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF service
 LANGUAGE sql
 STABLE
AS $function$
  SELECT s.*
  FROM rule_svc_resolved r JOIN service s ON (r.svc_id=s.svc_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed >= import_id)
  GROUP BY s.svc_id
  ORDER BY MAX(svc_name), s.svc_id
$function$;

CREATE OR REPLACE FUNCTION public.filter_rule_user_resolveds(management_row management, rule_ids bigint[], import_id bigint)
 RETURNS SETOF usr
 LANGUAGE sql
 STABLE
AS $function$
  SELECT u.*
  FROM rule_user_resolved r JOIN usr u ON (r.user_id=u.user_id)
  WHERE r.mgm_id = management_row.mgm_id AND rule_id = any (rule_ids) AND r.created <= import_id AND (r.removed IS NULL OR r.removed >= import_id)
  GROUP BY u.user_id
  ORDER BY MAX(user_name), u.user_id
$function$;


CREATE OR REPLACE FUNCTION ip_ranges_overlap(ip1_start cidr, ip1_end cidr, ip2_start cidr, ip2_end cidr, inverted boolean DEFAULT FALSE)
    RETURNS boolean AS $$
    BEGIN
        IF ip1_start IS NULL OR ip1_end IS NULL OR ip2_start IS NULL OR ip2_end IS NULL THEN
            RETURN FALSE;
        END IF;

        IF inverted THEN                                            -- []: cidr1 ~> invert (): cidr2
            IF ip1_start <= ip2_start AND ip2_end <= ip1_end THEN   --[-*(--)-*]--  ~>  --]-*(--)-*[--
                RETURN FALSE;
            ELSE
                RETURN TRUE;
            END IF;
        END IF;

        RETURN ip1_start <= ip2_end AND ip2_start <= ip1_end;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION has_relevant_change(cl_rule changelog_rule, hasura_session json)
RETURNS boolean AS $$
    DECLARE t_id integer;
    show boolean DEFAULT false;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show := true;
        ELSE
            IF EXISTS (
                SELECT diff.obj_id, diff.negated FROM ( -- set of difference between rule_from of old and new rule
                    SELECT obj_id, negated FROM rule_from WHERE rule_id = cl_rule.old_rule_id EXCEPT SELECT obj_id FROM rule_from WHERE rule_id = cl_rule.new_rule_id
                    UNION
                    (SELECT obj_id, negated FROM rule_from WHERE rule_id = cl_rule.new_rule_id EXCEPT SELECT obj_id FROM rule_from WHERE rule_id = cl_rule.old_rule_id)
                ) AS diff
                JOIN objgrp_flat ON (obj_id=objgrp_flat_id)
                JOIN object ON (objgrp_flat_member_id=object.obj_id)
                JOIN tenant_network ON
                    (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, diff.negated))
                WHERE tenant_id = t_id
            ) THEN
                show := true;
            END IF;

            IF EXISTS (
                SELECT diff.obj_id, diff.negated FROM ( -- set of difference between rule_to of old and new rule
                    SELECT obj_id, negated FROM rule_to WHERE rule_id = cl_rule.old_rule_id EXCEPT SELECT obj_id FROM rule_to WHERE rule_id = cl_rule.new_rule_id
                    UNION
                    (SELECT obj_id, negated FROM rule_to WHERE rule_id = cl_rule.new_rule_id EXCEPT SELECT obj_id FROM rule_to WHERE rule_id = cl_rule.old_rule_id)
                ) AS diff
                JOIN objgrp_flat ON (obj_id=objgrp_flat_id)
                JOIN object ON (objgrp_flat_member_id=object.obj_id)
                JOIN tenant_network ON
                    (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, diff.negated))
                WHERE tenant_id = t_id
            ) THEN
                show := true;
            END IF;

        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION rule_relevant_for_tenant(rule rule, hasura_session json)
RETURNS boolean AS $$
    DECLARE t_id integer;
    show boolean DEFAULT false;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show := true;
        ELSE
            IF EXISTS (
                SELECT rf.obj_id FROM rule_from rf
                    LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated))
                WHERE rf.rule_id = rule.rule_id AND tenant_id = t_id
            ) THEN
                show := true;
            ELSIF EXISTS (
                SELECT rt.obj_id FROM rule_to rt
                    LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
                WHERE rt.rule_id = rule.rule_id AND tenant_id = t_id
            ) THEN
                show := true;
            END IF;

        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION rule_from_relevant_for_tenant(rule_from rule_from, hasura_session json)
RETURNS boolean AS $$
    DECLARE t_id integer;
    show boolean DEFAULT false;
    rule_to_obj RECORD;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show := true;
        ELSE
            IF EXISTS ( -- ip of rule_from object is in tenant_network of tenant
                SELECT rf.obj_id FROM rule_from rf
                    LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated))
                WHERE rule_from_id = rule_from.rule_from_id AND tenant_id = t_id --> this better be efficient (rule_from_id checked before join) (!TODO: check this)
            ) THEN
                show := true;
            ELSE -- check if all rule_from objects visible since relevant rule_to exists
                FOR rule_to_obj IN
                    SELECT rt.*, tenant_network.tenant_id
                    FROM rule_to rt
                        LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat_id)
                        LEFT JOIN object ON (objgrp_flat_member_id=object.obj_id)
                        LEFT JOIN tenant_network ON
                            (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
                    WHERE rule_id = rule_from.rule_id
                LOOP
                    IF rule_to_obj.tenant_id = t_id THEN
                        show := true;
                        EXIT;
                    END IF;
                END LOOP;
            END IF;

        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION rule_to_relevant_for_tenant(rule_to rule_to, hasura_session json)
RETURNS boolean AS $$
    DECLARE t_id integer;
    show boolean DEFAULT false;
    rule_from_obj RECORD;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show := true;
        ELSE
            IF EXISTS ( -- ip of rule_to object is in tenant_network of tenant
                SELECT rt.obj_id FROM rule_to rt
                    LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
                WHERE rule_to_id = rule_to.rule_to_id AND tenant_id = t_id --> this better be efficient (rule_to_id checked before join) (!TODO: check this)
            ) THEN
                show := true;
            ELSE -- check if all rule_to objects visible since relevant rule_from exists
                FOR rule_from_obj IN
                    SELECT rf.*, tenant_network.tenant_id
                    FROM rule_from rf
                        LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat_id)
                        LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                        LEFT JOIN tenant_network ON
                            (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated))
                    WHERE rule_id = rule_to.rule_id
                LOOP
                    IF rule_from_obj.tenant_id = t_id THEN
                        show := true;
                        EXIT;
                    END IF;
                END LOOP;
            END IF;

        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION object_relevant_for_tenant(object object, hasura_session json) -- todo: try over all objects in rule_from and rule_to
RETURNS boolean AS $$
    DECLARE t_id integer;
    show boolean DEFAULT false;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id = 1 THEN
            show := true;
        ELSIF EXISTS ( -- ip of object is in tenant_network of tenant
            SELECT o.obj_id FROM object o
                LEFT JOIN tenant_network ON
                    (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end))
            WHERE obj_id = object.obj_id AND tenant_id = t_id
        ) THEN
            show := true;
        ELSIF EXISTS ( -- object is in rule_from or rule_to of a rule that is visible to tenant
            SELECT r.rule_id from rule r
                LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
                LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
                LEFT JOIN objgrp_flat rf_of ON (rf.obj_id=rf_of.objgrp_flat_id)
                LEFT JOIN objgrp_flat rt_of ON (rt.obj_id=rt_of.objgrp_flat_id)
                LEFT JOIN object rf_o ON (rf_of.objgrp_flat_member_id=rf_o.obj_id)
                LEFT JOIN object rt_o ON (rt_of.objgrp_flat_member_id=rt_o.obj_id)
                LEFT JOIN tenant_network ON
                    (ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated)
                     OR ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
            WHERE (rf_o.obj_id = object.obj_id OR rt_o.obj_id = object.obj_id) AND tenant_id = t_id AND r.rule_head_text is NULL
        ) THEN
            show := true;
        END IF;

        RETURN show;
    END;
$$ LANGUAGE 'plpgsql' STABLE;



CREATE OR REPLACE FUNCTION get_objects_for_tenant(management_row management, tenant integer, hasura_session json)
RETURNS SETOF object AS $$
    DECLARE t_id integer;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1 THEN
            RAISE EXCEPTION 'Tenant id in hasura session is not 1 (admin). Tenant simulation not allowed.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant 1 (admin) cannot be simulated.';
        ELSE
            RETURN QUERY
                SELECT o.* FROM (
                    SELECT o.* FROM object o
                        LEFT JOIN rule_from rf ON (o.obj_id=rf.obj_id)
                        LEFT JOIN rule r ON (rf.rule_id=r.rule_id)
                        LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
                        LEFT JOIN objgrp_flat rt_of ON (rt.obj_id=rt_of.objgrp_flat_id)
                        LEFT JOIN object rt_o ON (rt_of.objgrp_flat_member_id=rt_o.obj_id)
                        LEFT JOIN tenant_network ON
                            (ip_ranges_overlap(o.obj_ip, o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated)
                             OR ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
                    WHERE o.mgm_id = management_row.mgm_id AND tenant_id = tenant AND r.rule_head_text is NULL
                    UNION
                    SELECT o.* FROM object o
                        LEFT JOIN rule_to rt ON (o.obj_id=rt.obj_id)
                        LEFT JOIN rule r ON (rt.rule_id=r.rule_id)
                        LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
                        LEFT JOIN objgrp_flat rf_of ON (rf.obj_id=rf_of.objgrp_flat_id)
                        LEFT JOIN object rf_o ON (rf_of.objgrp_flat_member_id=rf_o.obj_id)
                        LEFT JOIN tenant_network ON
                            (ip_ranges_overlap(o.obj_ip, o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated)
                             OR ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated))
                    WHERE o.mgm_id = management_row.mgm_id AND tenant_id = tenant AND r.rule_head_text is NULL
                ) AS o
                ORDER BY obj_name;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION get_rules_for_tenant(management_row management, tenant integer, hasura_session json)
RETURNS SETOF rule AS $$
    DECLARE t_id integer;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1 THEN
            RAISE EXCEPTION 'Tenant id in hasura session is not 1 (admin). Tenant simulation not allowed.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant 1 (admin) cannot be simulated.';
        ELSE
            RETURN QUERY
                SELECT r.* FROM rule r
                    LEFT JOIN rule_from rf ON (r.rule_id=rf.rule_id)
                    LEFT JOIN objgrp_flat rf_of ON (rf.obj_id=rf_of.objgrp_flat_id)
                    LEFT JOIN object rf_o ON (rf_of.objgrp_flat_member_id=rf_o.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(rf_o.obj_ip, rf_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated))
                WHERE r.mgm_id = management_row.mgm_id AND tenant_id = tenant
                UNION
                SELECT r.* FROM rule r
                    LEFT JOIN rule_to rt ON (r.rule_id=rt.rule_id)
                    LEFT JOIN objgrp_flat rt_of ON (rt.obj_id=rt_of.objgrp_flat_id)
                    LEFT JOIN object rt_o ON (rt_of.objgrp_flat_member_id=rt_o.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(rt_o.obj_ip, rt_o.obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
                WHERE r.mgm_id = management_row.mgm_id AND tenant_id = tenant
                ORDER BY rule_name;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION get_rule_froms_for_tenant(rule rule, tenant integer, hasura_session json)
RETURNS SETOF rule_from AS $$
    DECLARE t_id integer;
    
    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1 THEN
            RAISE EXCEPTION 'Tenant id in hasura session is not 1 (admin). Tenant simulation not allowed.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant 1 (admin) cannot be simulated.';
        ELSIF EXISTS (
            SELECT rt.obj_id FROM rule_to rt
                LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat.objgrp_flat_id)
                LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
            WHERE rt.rule_id = rule.rule_id AND tenant_id = tenant
        ) THEN
            RETURN QUERY
                SELECT rf.* FROM rule_from rf WHERE rule_id = rule.rule_id;
        ELSE
            RETURN QUERY
                SELECT rf.* FROM rule_from rf
                    LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated))
                WHERE rule_id = rule.rule_id AND tenant_id = tenant;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;


CREATE OR REPLACE FUNCTION get_rule_tos_for_tenant(rule rule, tenant integer, hasura_session json)
RETURNS SETOF rule_to AS $$
    DECLARE t_id integer;

    BEGIN
        t_id := (hasura_session ->> 'x-hasura-tenant-id')::integer;

        IF t_id IS NULL THEN
            RAISE EXCEPTION 'No tenant id found in hasura session'; --> only happens when using auth via x-hasura-admin-secret (no tenant id is set)
        ELSIF t_id != 1 THEN
            RAISE EXCEPTION 'Tenant id in hasura session is not 1 (admin). Tenant simulation not allowed.';
        ELSIF tenant = 1 THEN
            RAISE EXCEPTION 'Tenant 1 (admin) cannot be simulated.';
        ELSIF EXISTS (
            SELECT rf.obj_id FROM rule_from rf
                LEFT JOIN objgrp_flat ON (rf.obj_id=objgrp_flat.objgrp_flat_id)
                LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                LEFT JOIN tenant_network ON
                    (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rf.negated))
            WHERE rf.rule_id = rule.rule_id AND tenant_id = tenant
        ) THEN
            RETURN QUERY
                SELECT rt.* FROM rule_to rt WHERE rule_id = rule.rule_id;
        ELSE
            RETURN QUERY
                SELECT rt.* FROM rule_to rt
                    LEFT JOIN objgrp_flat ON (rt.obj_id=objgrp_flat.objgrp_flat_id)
                    LEFT JOIN object ON (objgrp_flat.objgrp_flat_member_id=object.obj_id)
                    LEFT JOIN tenant_network ON
                        (ip_ranges_overlap(obj_ip, obj_ip_end, tenant_net_ip, tenant_net_ip_end, rt.negated))
                WHERE rule_id = rule.rule_id AND tenant_id = tenant;
        END IF;
    END;
$$ LANGUAGE 'plpgsql' STABLE;

------------------------------------------------------------------------------------------------------------------------
-- rule_relevant complexity: O(rf + rt)
-- rule_from_relevant complexity: O(rt)
-- rule_to_relevant complexity: O(rf)
-- total for single rule: O(rf + rt + 2*rf*rt)
--  theoretical min needed complexity: O(2(rf+rt))
-- obj_relevant complexity: O(r * rf * rt)
-- with material view: all O(1) but additional O(ten * r * (rf + rt)) for each import / tenant change
