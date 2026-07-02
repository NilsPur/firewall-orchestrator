using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json.Serialization;

namespace FWO.Data.Ai
{
    /// <summary>
    /// An AI assistant chat session. Conversation history is persisted as the opaque serialized
    /// Microsoft Agent Framework session in <see cref="State"/>; provider and model are fixed when
    /// the session is created.
    /// </summary>
    public class AiSession
    {
        [JsonProperty("id"), JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonProperty("user_id"), JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonProperty("name"), JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonProperty("created"), JsonPropertyName("created")]
        public DateTime Created { get; set; }

        [JsonProperty("system_prompt"), JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = "";

        [JsonProperty("model_id"), JsonPropertyName("model_id")]
        public string ModelId { get; set; } = "";

        [JsonProperty("provider_id"), JsonPropertyName("provider_id")]
        public long ProviderId { get; set; }

        /// <summary>
        /// The serialized Microsoft Agent Framework session (chat history and thread state).
        /// Populated from the database via GraphQL and used server-side only; it is never exposed
        /// over the REST API.
        /// </summary>
        [JsonProperty("state")]
        [System.Text.Json.Serialization.JsonIgnore]
        public JToken State { get; set; } = new JObject();
    }

    /// <summary>
    /// A single chat message projected from a session for display in the assistant timeline.
    /// </summary>
    public class AiChatMessage
    {
        [JsonProperty("role"), JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonProperty("content"), JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }
}
