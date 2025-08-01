﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Data
{
    public class Rule
    {
        [JsonProperty("rule_id"), JsonPropertyName("rule_id")]
        public long Id { get; set; }

        [JsonProperty("rule_uid"), JsonPropertyName("rule_uid")]
        public string? Uid { get; set; } = "";

        [JsonProperty("mgm_id"), JsonPropertyName("mgm_id")]
        public int MgmtId { get; set; }

        [JsonProperty("rule_num_numeric"), JsonPropertyName("rule_num_numeric")]
        public double OrderNumber { get; set; }

        [JsonProperty("rule_name"), JsonPropertyName("rule_name")]
        public string? Name { get; set; } = "";

        [JsonProperty("rule_comment"), JsonPropertyName("rule_comment")]
        public string? Comment { get; set; } = "";

        [JsonProperty("rule_disabled"), JsonPropertyName("rule_disabled")]
        public bool Disabled { get; set; }

        [JsonProperty("rule_services"), JsonPropertyName("rule_services")]
        public ServiceWrapper[] Services { get; set; } = [];

        [JsonProperty("rule_svc_neg"), JsonPropertyName("rule_svc_neg")]
        public bool ServiceNegated { get; set; }

        [JsonProperty("rule_svc"), JsonPropertyName("rule_svc")]
        public string Service { get; set; } = "";

        [JsonProperty("rule_src_neg"), JsonPropertyName("rule_src_neg")]
        public bool SourceNegated { get; set; }

        [JsonProperty("rule_src"), JsonPropertyName("rule_src")]
        public string Source { get; set; } = "";

        [JsonProperty("src_zone"), JsonPropertyName("src_zone")]
        public NetworkZone? SourceZone { get; set; } = new ();

        [JsonProperty("rule_froms"), JsonPropertyName("rule_froms")]
        public NetworkLocation[] Froms { get; set; } = [];
      
        [JsonProperty("rule_dst_neg"), JsonPropertyName("rule_dst_neg")]
        public bool DestinationNegated { get; set; }

        [JsonProperty("rule_dst"), JsonPropertyName("rule_dst")]
        public string Destination { get; set; } = "";

        [JsonProperty("dst_zone"), JsonPropertyName("dst_zone")]
        public NetworkZone? DestinationZone { get; set; } = new ();

        [JsonProperty("rule_tos"), JsonPropertyName("rule_tos")]
        public NetworkLocation[] Tos { get; set; } = [];

        [JsonProperty("rule_action"), JsonPropertyName("rule_action")]
        public string Action { get; set; } = "";

        [JsonProperty("rule_track"), JsonPropertyName("rule_track")]
        public string Track { get; set; } = "";

        [JsonProperty("section_header"), JsonPropertyName("section_header")]
        public string? SectionHeader { get; set; } = "";

        [JsonProperty("rule_metadatum"), JsonPropertyName("rule_metadatum")]
        public RuleMetadata Metadata {get; set;} = new ();

        [JsonProperty("translate"), JsonPropertyName("translate")]
        public NatData NatData {get; set;} = new ();

        [JsonProperty("owner_name"), JsonPropertyName("owner_name")]
        public string OwnerName {get; set;} = "";

        [JsonProperty("owner_id"), JsonPropertyName("owner_id")]
        public int? OwnerId {get; set;}

        [JsonProperty("matches"), JsonPropertyName("matches")]
        public string IpMatch {get; set;} = "";

        [JsonProperty("dev_id"), JsonPropertyName("dev_id")]
        public int DeviceId { get; set; }

        [JsonProperty("rule_custom_fields"), JsonPropertyName("rule_custom_fields")]
        public string CustomFields { get; set; } = "";


        public int DisplayOrderNumber { get; set; }
        public bool Certified { get; set; }
        public string ManagementName = "";
        public string DeviceName { get; set; } = "";
        public NetworkLocation[] DisregardedFroms { get; set; } = [];
        public NetworkLocation[] DisregardedTos { get; set; } = [];
        public NetworkService[] DisregardedServices { get; set; } = [];
        public bool ShowDisregarded { get; set; } = false;
        public long ConnId;
        public bool ModellFound = false;
        public bool ModellOk = false;
        public bool Detailed = false;
        public List<string> UnusedSpecialUserObjects = [];
        public List<string> UnusedUpdatableObjects = [];

        public Rule()
        { }

        public Rule(Rule rule)
        {
            Id = rule.Id;
            Uid = rule.Uid;
            MgmtId = rule.MgmtId;
            OrderNumber = rule.OrderNumber;
            Name = rule.Name;
            Comment = rule.Comment;
            Disabled = rule.Disabled;
            Services = rule.Services;
            ServiceNegated = rule.ServiceNegated;
            Service = rule.Service;
            SourceNegated = rule.SourceNegated;
            Source = rule.Source;
            SourceZone = rule.SourceZone;
            Froms = rule.Froms;
            DestinationNegated = rule.DestinationNegated;
            Destination = rule.Destination;
            DestinationZone = rule.DestinationZone;
            Tos = rule.Tos;
            Action = rule.Action;
            Track = rule.Track;
            SectionHeader = rule.SectionHeader;
            Metadata = rule.Metadata;
            NatData = rule.NatData;
            OwnerName = rule.OwnerName;
            OwnerId = rule.OwnerId;
            IpMatch = rule.IpMatch;
            DeviceId = rule.DeviceId;
            CustomFields = rule.CustomFields;
            DisplayOrderNumber = rule.DisplayOrderNumber;
            Certified = rule.Certified;
            ManagementName = rule.ManagementName;
            DeviceName = rule.DeviceName;
            DisregardedFroms = rule.DisregardedFroms;
            DisregardedTos = rule.DisregardedTos;
            DisregardedServices = rule.DisregardedServices;
            ShowDisregarded = rule.ShowDisregarded;
            ConnId = rule.ConnId;
            ModellFound = rule.ModellFound;
            ModellOk = rule.ModellOk;
            Detailed = rule.Detailed;
            UnusedSpecialUserObjects = rule.UnusedSpecialUserObjects;
            UnusedUpdatableObjects = rule.UnusedUpdatableObjects;
        }

        public bool IsDropRule()
        {
            return Action == "drop" || Action == "reject" || Action == "deny";
        }
    }
}
