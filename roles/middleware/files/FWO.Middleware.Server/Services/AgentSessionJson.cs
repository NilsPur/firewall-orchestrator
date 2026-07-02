using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// JSON settings for persisted Microsoft Agent Framework session state.
    /// </summary>
    internal static class AgentSessionJson
    {
        /// <summary>
        /// Options used when reading framework state from PostgreSQL jsonb.
        /// </summary>
        internal static readonly JsonSerializerOptions Options = new()
        {
            AllowOutOfOrderMetadataProperties = true,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        /// <summary>
        /// Reorders metadata properties before deserializing state read back from PostgreSQL jsonb.
        /// </summary>
        internal static JsonElement PrepareForDeserialize(JsonElement state)
        {
            return Transform(state, removeSystemMessages: false);
        }

        /// <summary>
        /// Removes transient system messages and keeps metadata first before persisting state.
        /// </summary>
        internal static JsonElement PrepareForPersist(JsonElement state)
        {
            return Transform(state, removeSystemMessages: true);
        }

        private static JsonElement Transform(JsonElement state, bool removeSystemMessages)
        {
            JsonNode? node = JsonNode.Parse(state.GetRawText());
            JsonNode? transformed = TransformNode(node, removeSystemMessages);
            using JsonDocument document = JsonDocument.Parse(transformed?.ToJsonString(Options) ?? "null");
            return document.RootElement.Clone();
        }

        private static JsonNode? TransformNode(JsonNode? node, bool removeSystemMessages)
        {
            return node switch
            {
                JsonObject jsonObject => TransformObject(jsonObject, removeSystemMessages),
                JsonArray jsonArray => TransformArray(jsonArray, removeSystemMessages),
                null => null,
                _ => node.DeepClone()
            };
        }

        private static JsonObject TransformObject(JsonObject jsonObject, bool removeSystemMessages)
        {
            JsonObject transformed = [];
            AddProperties(jsonObject, transformed, removeSystemMessages, metadataProperties: true);
            AddProperties(jsonObject, transformed, removeSystemMessages, metadataProperties: false);
            return transformed;
        }

        private static JsonArray TransformArray(JsonArray jsonArray, bool removeSystemMessages)
        {
            JsonArray transformed = [];
            foreach (JsonNode? item in jsonArray)
            {
                if (removeSystemMessages && item is JsonObject itemObject && IsSystemChatMessage(itemObject))
                {
                    continue;
                }
                transformed.Add(TransformNode(item, removeSystemMessages));
            }
            return transformed;
        }

        private static void AddProperties(JsonObject source, JsonObject target, bool removeSystemMessages, bool metadataProperties)
        {
            foreach (KeyValuePair<string, JsonNode?> property in source)
            {
                if (property.Key.StartsWith('$') != metadataProperties)
                {
                    continue;
                }
                target[property.Key] = TransformNode(property.Value, removeSystemMessages);
            }
        }

        private static bool IsSystemChatMessage(JsonObject jsonObject)
        {
            return TryGetString(jsonObject, "role", out string role)
                && string.Equals(role, "system", StringComparison.OrdinalIgnoreCase)
                && (ContainsProperty(jsonObject, "contents") || ContainsProperty(jsonObject, "content") || ContainsProperty(jsonObject, "text"));
        }

        private static bool TryGetString(JsonObject jsonObject, string propertyName, out string value)
        {
            value = "";
            KeyValuePair<string, JsonNode?> property = jsonObject.FirstOrDefault(property =>
                string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(property.Key))
            {
                return false;
            }
            value = property.Value?.GetValue<string>() ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool ContainsProperty(JsonObject jsonObject, string propertyName)
        {
            return jsonObject.Any(property => string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
