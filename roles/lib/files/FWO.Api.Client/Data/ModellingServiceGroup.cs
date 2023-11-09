﻿using System.Text.Json.Serialization; 
using Newtonsoft.Json;

namespace FWO.Api.Data
{
    public class ModellingServiceGroup : ModellingSvcElem
    {
        [JsonProperty("comment"), JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonProperty("creator"), JsonPropertyName("creator")]
        public string? Creator { get; set; }

        [JsonProperty("creation_date"), JsonPropertyName("creation_date")]
        public DateTime? CreationDate { get; set; }

        [JsonProperty("services"), JsonPropertyName("services")]
        public List<ModellingServiceWrapper> Services { get; set; } = new();


        public bool Sanitize()
        {
            bool shortened = base.Sanitize();
            Comment = Sanitizer.SanitizeCommentOpt(Comment, ref shortened);
            return shortened;
        }
    }

    public class ModellingServiceGroupWrapper
    {
        [JsonProperty("service_group"), JsonPropertyName("service_group")]
        public ModellingServiceGroup Content { get; set; } = new();

        public static ModellingServiceGroup[] Resolve(List<ModellingServiceGroupWrapper> wrappedList)
        {
            return Array.ConvertAll(wrappedList.ToArray(), wrapper => wrapper.Content);
        }
    }
}
