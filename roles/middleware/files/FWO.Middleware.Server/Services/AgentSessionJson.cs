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
        /// Options used when reading and writing framework state.
        /// </summary>
        internal static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        /// <summary>
        /// Removes transient system messages before persisting state.
        /// </summary>
        internal static JsonElement PrepareForPersist(JsonElement state)
        {
            JsonNode? node = JsonNode.Parse(state.GetRawText());
            JsonNode? transformed = TransformNode(node);
            using JsonDocument document = JsonDocument.Parse(transformed?.ToJsonString(Options) ?? "null");
            return document.RootElement.Clone();
        }

        private static JsonNode? TransformNode(JsonNode? node)
        {
            return node switch
            {
                JsonObject jsonObject => TransformObject(jsonObject),
                JsonArray jsonArray => TransformArray(jsonArray),
                null => null,
                _ => node.DeepClone()
            };
        }

        private static JsonObject TransformObject(JsonObject jsonObject)
        {
            JsonObject transformed = [];
            foreach (KeyValuePair<string, JsonNode?> property in jsonObject)
            {
                transformed[property.Key] = TransformNode(property.Value);
            }
            return transformed;
        }

        private static JsonArray TransformArray(JsonArray jsonArray)
        {
            JsonArray transformed = [];
            foreach (JsonNode? item in jsonArray)
            {
                if (item is JsonObject itemObject && IsSystemChatMessage(itemObject))
                {
                    continue;
                }
                transformed.Add(TransformNode(item));
            }
            return transformed;
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
