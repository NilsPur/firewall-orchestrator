-- Autodiscovery alerts for new managements carried the decrypted import credential of the super management in
-- json_data. Remove the secret from existing alerts; the autodiscovery dialog only needs the credential id.
-- json_data holds the management either as a JSON object or as a JSON string containing the object. Alerts with
-- plain text (e.g. an exception message) are left as they are. Running this again finds no secret and changes nothing.
DO $$
DECLARE
    alert_row RECORD;
    management_data jsonb;
BEGIN
    FOR alert_row IN
        SELECT alert_id, json_data FROM alert
        WHERE source = 'autodiscovery' AND json_data IS NOT NULL AND json_data::text LIKE '%secret%'
    LOOP
        BEGIN
            IF json_typeof(alert_row.json_data) = 'string' THEN
                management_data := (alert_row.json_data #>> '{}')::jsonb;
            ELSE
                management_data := alert_row.json_data::jsonb;
            END IF;
            IF jsonb_typeof(management_data) = 'object' AND management_data #> '{import_credential,secret}' IS NOT NULL THEN
                management_data := management_data #- '{import_credential,secret}';
                IF json_typeof(alert_row.json_data) = 'string' THEN
                    UPDATE alert SET json_data = to_json(management_data::text) WHERE alert_id = alert_row.alert_id;
                ELSE
                    UPDATE alert SET json_data = management_data::json WHERE alert_id = alert_row.alert_id;
                END IF;
            END IF;
        EXCEPTION WHEN invalid_text_representation THEN
            -- json_data is plain text, not a management
            NULL;
        END;
    END LOOP;
END $$;

-- AI settings (AI provider, model, credential of the API key and agent loop), edited on the AI settings page and read
-- by the AI harness with its technical user (role ai-harness), see FWO.Data.Ai.AiSettings. The view
-- v_ai_provider_credential is (re)defined by the idempotent fworch-ai.sql.
INSERT INTO config (config_key, config_value, config_user)
VALUES
    ('aiProvider', 'None', 0),
    ('aiModel', '', 0),
    ('aiEndpoint', '', 0),
    ('aiCredentialId', '0', 0),
    ('aiContextWindowTokens', '0', 0),
    ('aiMaxOutputTokens', '0', 0),
    ('aiMaxIterationsPerRequest', '10', 0),
    ('aiMaxConsecutiveErrorsPerRequest', '3', 0),
    ('aiCompactionReserveTokens', '0', 0)
ON CONFLICT (config_key, config_user) DO NOTHING;
