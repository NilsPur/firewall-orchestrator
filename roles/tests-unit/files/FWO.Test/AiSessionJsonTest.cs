using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Ai;
using FWO.Middleware.Server.Services;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FWO.Test
{
    [TestFixture]
    public class AiSessionJsonTest
    {
        [Test]
        public void AiSession_DeserializesStateFromGraphQl()
        {
            JObject payload = JObject.Parse("""
            {
              "id": 1,
              "user_id": 7,
              "provider_id": 3,
              "state": { "messages": [ { "role": "user", "content": "hello" } ] }
            }
            """);

            AiSession session = payload.ToObject<AiSession>()!;

            Assert.Multiple(() =>
            {
                Assert.That(session.ProviderId, Is.EqualTo(3));
                Assert.That(session.State["messages"]?[0]?["content"]?.Value<string>(), Is.EqualTo("hello"));
            });
        }

        [Test]
        public void AiSession_OmitsStateFromRestSerialization()
        {
            AiSession session = new()
            {
                Id = 1,
                UserId = 7,
                ProviderId = 3,
                State = JObject.Parse("{\"messages\":[]}")
            };

            string serialized = JsonSerializer.Serialize(session);

            // The opaque framework state is server-side only and must never be exposed over REST.
            Assert.That(serialized, Does.Not.Contain("\"state\""));
            Assert.That(serialized, Does.Not.Contain("messages"));
        }

        [Test]
        public void SaveAiSessionState_QueryUsesStateVariable()
        {
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("$state: jsonb!"));
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("$userId: Int!"));
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("user_id: {_eq: $userId}"));
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("state: $state"));
            Assert.That(AiQueries.saveAiSessionState, Does.Not.Contain("last_updated"));
        }

        [Test]
        public void AgentSessionJson_AllowsJsonbReorderedTypeMetadata()
        {
            const string jsonbReorderedJson = """{"value":"hello","$type":"derived"}""";

            Assert.Catch(() => JsonSerializer.Deserialize<PolymorphicBase>(jsonbReorderedJson));

            using JsonDocument document = JsonDocument.Parse(jsonbReorderedJson);
            JsonElement normalized = AgentSessionJson.PrepareForDeserialize(document.RootElement);
            PolymorphicBase? deserialized = JsonSerializer.Deserialize<PolymorphicBase>(normalized, AgentSessionJson.Options);

            Assert.That(deserialized, Is.TypeOf<PolymorphicDerived>());
            Assert.That(deserialized?.Value, Is.EqualTo("hello"));
        }

        [Test]
        public void AgentSessionJson_UsesReflectionMetadataResolver()
        {
            Assert.That(AgentSessionJson.Options.TypeInfoResolver, Is.Not.Null);
        }

        [Test]
        public void AgentSessionJson_RemovesSystemMessagesBeforePersist()
        {
            using JsonDocument document = JsonDocument.Parse("""
            {
              "messages": [
                { "role": "system", "content": "do not persist" },
                { "role": "user", "content": "keep me" }
              ]
            }
            """);

            JsonElement persisted = AgentSessionJson.PrepareForPersist(document.RootElement);
            string persistedJson = persisted.GetRawText();

            Assert.Multiple(() =>
            {
                Assert.That(persistedJson, Does.Not.Contain("do not persist"));
                Assert.That(persistedJson, Does.Contain("keep me"));
            });
        }

        [Test]
        public void FwoRoutingAgent_PrependsConfiguredPromptAsSystemMessage()
        {
            List<ChatMessage> messages = [new(ChatRole.User, "hello")];

            List<ChatMessage> routedMessages = FwoRoutingAgent.PrependSystemPrompt(messages, "configured prompt");

            Assert.Multiple(() =>
            {
                Assert.That(routedMessages, Has.Count.EqualTo(2));
                Assert.That(routedMessages[0].Role, Is.EqualTo(ChatRole.System));
                Assert.That(routedMessages[0].Text, Is.EqualTo("configured prompt"));
                Assert.That(routedMessages[1].Role, Is.EqualTo(ChatRole.User));
            });
        }

        [Test]
        public void RenameAiSession_QueryUsesRenameOperationName()
        {
            Assert.That(AiQueries.renameAiSession, Does.Contain("mutation renameAiSession"));
            Assert.That(AiQueries.renameAiSession, Does.Contain("update_ai_session_by_pk"));
        }

        [Test]
        public void DeleteAiSession_QueryUsesRealDelete()
        {
            Assert.That(AiQueries.deleteAiSession, Does.Contain("delete_ai_session_by_pk"));
            Assert.That(AiQueries.deleteAiSession, Does.Not.Contain("update_ai_session_by_pk"));
            Assert.That(AiQueries.deleteAiSession, Does.Not.Contain("deleted: true"));
        }

        [Test]
        public async Task SaveState_SendsStateVariable()
        {
            AiSessionTestApiConnection apiConnection = new();
            AiSessionService service = new(apiConnection, new AiSettingsService(apiConnection));

            bool saved = await service.SaveState(5, 7, JObject.Parse("{\"messages\":[]}"));

            Assert.That(saved, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AiQueries.saveAiSessionState));
            Assert.That(GetVariable<long>(apiConnection.LastVariables!, "id"), Is.EqualTo(5));
            Assert.That(GetVariable<int>(apiConnection.LastVariables!, "userId"), Is.EqualTo(7));
            Assert.That(GetVariable<JToken>(apiConnection.LastVariables!, "state"), Is.Not.Null);
        }

        [Test]
        public async Task RenameSession_SendsRenameQuery()
        {
            AiSessionTestApiConnection apiConnection = new();
            AiSessionService service = new(apiConnection, new AiSettingsService(apiConnection));

            bool renamed = await service.RenameSession(5, 7, "Renamed");

            Assert.That(renamed, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AiQueries.renameAiSession));
            Assert.That(GetVariable<long>(apiConnection.LastVariables!, "id"), Is.EqualTo(5));
            Assert.That(GetVariable<string>(apiConnection.LastVariables!, "name"), Is.EqualTo("Renamed"));
        }

        [Test]
        public async Task DeleteSession_SendsDeleteQuery()
        {
            AiSessionTestApiConnection apiConnection = new();
            AiSessionService service = new(apiConnection, new AiSettingsService(apiConnection));

            bool deleted = await service.DeleteSession(5, 7);

            Assert.That(deleted, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AiQueries.deleteAiSession));
            Assert.That(GetVariable<long>(apiConnection.LastVariables!, "id"), Is.EqualTo(5));
        }

        [Test]
        public void ResolveProviderModel_ReturnsEnabledProviderAndModel()
        {
            AiSettings settings = BuildSettings(providerEnabled: true, modelEnabled: true);

            (AiProviderConfig provider, AiModelConfig model) = AgentFactoryService.ResolveProviderModel(settings, 1, "llama");

            Assert.That(provider.Kind, Is.EqualTo(AiProviderKind.Ollama));
            Assert.That(model.ModelId, Is.EqualTo("llama"));
        }

        [Test]
        public void ResolveProviderModel_ThrowsWhenModelDisabled()
        {
            AiSettings settings = BuildSettings(providerEnabled: true, modelEnabled: false);

            Assert.Throws<InvalidOperationException>(() => AgentFactoryService.ResolveProviderModel(settings, 1, "llama"));
        }

        [Test]
        public void ResolveModelSelection_ReturnsProviderForEnabledModelOnly()
        {
            Assert.That(AiSettingsService.ResolveModelSelection(BuildSettings(providerEnabled: true, modelEnabled: true), 1, "llama")?.Provider.Kind, Is.EqualTo(AiProviderKind.Ollama));
            Assert.That(AiSettingsService.ResolveModelSelection(BuildSettings(providerEnabled: true, modelEnabled: false), 1, "llama"), Is.Null);
            Assert.That(AiSettingsService.ResolveModelSelection(BuildSettings(providerEnabled: false, modelEnabled: true), 1, "llama"), Is.Null);
        }

        private static AiSettings BuildSettings(bool providerEnabled, bool modelEnabled)
        {
            return new AiSettings
            {
                Providers =
                [
                    new AiProviderConfig
                    {
                        Id = 1,
                        Kind = AiProviderKind.Ollama,
                        Enabled = providerEnabled,
                        Models = [new AiModelConfig { ModelId = "llama", ProviderKind = AiProviderKind.Ollama, Enabled = modelEnabled }]
                    }
                ]
            };
        }

        private static T GetVariable<T>(object variables, string name)
        {
            object? value = variables.GetType().GetProperty(name)?.GetValue(variables);
            Assert.That(value, Is.Not.Null);
            return (T)value!;
        }

        [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
        [JsonDerivedType(typeof(PolymorphicDerived), "derived")]
        private abstract class PolymorphicBase
        {
            public string Value { get; set; } = "";
        }

        private sealed class PolymorphicDerived : PolymorphicBase
        {
        }

        private sealed class AiSessionTestApiConnection : ApiConnection
        {
            public string LastQuery { get; private set; } = "";

            public object? LastVariables { get; private set; }

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(
                string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                object result = typeof(QueryResponseType) == typeof(ReturnId)
                    ? new ReturnId
                    {
                        UpdatedIdLong = GetOptionalVariable<long>(variables, "id"),
                        DeletedIdLong = GetOptionalVariable<long>(variables, "id"),
                        AffectedRows = 3
                    }
                    : typeof(QueryResponseType) == typeof(ReturnIdWrapper)
                        ? BuildReturnIdWrapper(query, variables)
                        : typeof(QueryResponseType) == typeof(List<AiSession>)
                            ? new List<AiSession>
                            {
                                new()
                                {
                                    Id = GetOptionalVariable<long>(variables, "id"),
                                    UserId = 7,
                                    Name = "Session"
                                }
                            }
                            : Activator.CreateInstance<QueryResponseType>()!;
                return Task.FromResult((QueryResponseType)result);
            }

            private static ReturnIdWrapper BuildReturnIdWrapper(string query, object? variables)
            {
                return query == AiQueries.saveAiSessionState
                    ? new ReturnIdWrapper { ReturnIds = [new ReturnId { UpdatedIdLong = GetOptionalVariable<long>(variables, "id") }] }
                    : new ReturnIdWrapper { ReturnIds = [new ReturnId { NewIdLong = 42 }] };
            }

            public override void SetAuthHeader(string jwt)
            {
            }

            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            public override void SetRole(string role)
            {
            }

            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList)
            {
            }

            public override void SwitchBack()
            {
            }

            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(
                string query, object? variables = null, string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription,
                object? variables = null,
                string? operationName = null)
            {
                throw new NotImplementedException();
            }

            public override void DisposeSubscriptions<T>()
            {
            }

            protected override void Dispose(bool disposing)
            {
            }

            private static T GetOptionalVariable<T>(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value == null ? default! : (T)value;
            }
        }
    }
}
