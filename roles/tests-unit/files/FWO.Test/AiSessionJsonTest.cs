using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Ai;
using FWO.Middleware.Server.Services;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Text.Json;

namespace FWO.Test
{
    [TestFixture]
    public class AiSessionJsonTest
    {
        [Test]
        public void AiSession_DeserializesRawStateFromGraphQl()
        {
            JObject payload = JObject.Parse("""
            {
              "id": 1,
              "user_id": 7,
              "state": "{\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}"
            }
            """);

            AiSession session = payload.ToObject<AiSession>()!;

            Assert.Multiple(() =>
            {
                Assert.That(session.State, Does.Contain("hello"));
            });
        }

        [Test]
        public void AiSession_OmitsStateFromRestSerialization()
        {
            AiSession session = new()
            {
                Id = 1,
                UserId = 7,
                State = "{\"messages\":[]}"
            };

            string serialized = JsonSerializer.Serialize(session);

            Assert.That(serialized, Does.Not.Contain("\"state\""));
            Assert.That(serialized, Does.Not.Contain("messages"));
        }

        [Test]
        public void SaveAiSessionState_QueryUsesStringStateVariable()
        {
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("$state: String!"));
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("$userId: Int!"));
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("user_id: {_eq: $userId}"));
            Assert.That(AiQueries.saveAiSessionState, Does.Contain("state: $state"));
            Assert.That(AiQueries.saveAiSessionState, Does.Not.Contain("jsonb"));
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
        public void BuildChatOptions_UsesConfiguredPromptAsInstructions()
        {
            ChatOptions options = AgentFactoryService.BuildChatOptions(new AiModelConfig(), null, "configured prompt");

            Assert.That(options.Instructions, Is.EqualTo("configured prompt"));
        }

        [Test]
        public async Task SaveState_SendsRawStateVariable()
        {
            AiSessionTestApiConnection apiConnection = new();
            AiSessionService service = new(apiConnection, new AiSettingsService(apiConnection));
            const string state = "{\"messages\":[]}";

            bool saved = await service.SaveState(5, 7, state);

            Assert.That(saved, Is.True);
            Assert.That(apiConnection.LastQuery, Is.EqualTo(AiQueries.saveAiSessionState));
            Assert.That(GetVariable<long>(apiConnection.LastVariables!, "id"), Is.EqualTo(5));
            Assert.That(GetVariable<int>(apiConnection.LastVariables!, "userId"), Is.EqualTo(7));
            Assert.That(GetVariable<string>(apiConnection.LastVariables!, "state"), Is.EqualTo(state));
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

        private static T GetVariable<T>(object variables, string name)
        {
            object? value = variables.GetType().GetProperty(name)?.GetValue(variables);
            Assert.That(value, Is.Not.Null);
            return (T)value!;
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

            public override void SetAuthHeader(string jwt) { }
            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => Task.CompletedTask;
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }
            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(
                string query, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(
                Action<Exception> exceptionHandler,
                GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler,
                string subscription,
                object? variables = null,
                string? operationName = null) => throw new NotImplementedException();
            public override void DisposeSubscriptions<T>() { }
            protected override void Dispose(bool disposing) { }

            private static T GetOptionalVariable<T>(object? variables, string name)
            {
                object? value = variables?.GetType().GetProperty(name)?.GetValue(variables);
                return value == null ? default! : (T)value;
            }
        }
    }
}
