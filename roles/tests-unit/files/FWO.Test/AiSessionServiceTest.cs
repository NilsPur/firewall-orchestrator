using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Ai;
using FWO.Middleware.Server.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AiSessionServiceTest
    {
        [Test]
        public async Task CreateSession_PersistsSingleModel()
        {
            CapturingApiConnection apiConnection = new()
            {
                ConfigResponseJson = "[]",
                ProviderResponseJson = """
                [
                  {
                    "id": 1,
                    "kind": "OpenAi",
                    "display_name": "OpenAI",
                    "models": [ { "model_id": "gpt-5.4-mini", "enabled": true } ]
                  }
                ]
                """
            };
            AiSessionService sessionService = new(apiConnection, new AiSettingsService(apiConnection));

            AiSession session = await sessionService.CreateSession(1, null);

            object variables = apiConnection.VariablesByQuery[AiQueries.addAiSession];
            Assert.Multiple(() =>
            {
                Assert.That(GetVariable<string>(variables, "modelId"), Is.EqualTo("gpt-5.4-mini"));
                Assert.That(session.ModelId, Is.EqualTo("gpt-5.4-mini"));
            });
        }

        [Test]
        public void CreateSession_RejectsInactiveAssistant()
        {
            CapturingApiConnection apiConnection = new()
            {
                ConfigResponseJson = """[{ "config_key": "aiAssistantActive", "config_value": "false" }]""",
                ProviderResponseJson = """
                [
                  {
                    "id": 1,
                    "kind": "OpenAi",
                    "display_name": "OpenAI",
                    "models": [ { "model_id": "gpt-5.4-mini", "enabled": true } ]
                  }
                ]
                """
            };
            AiSessionService sessionService = new(apiConnection, new AiSettingsService(apiConnection));

            Assert.That(async () => await sessionService.CreateSession(1, null), Throws.InvalidOperationException);
        }

        [Test]
        public async Task SaveState_ScopesMutationToUser()
        {
            CapturingApiConnection apiConnection = new();
            AiSessionService sessionService = new(apiConnection, new AiSettingsService(apiConnection));
            const string state = """{"messages":[]}""";

            bool saved = await sessionService.SaveState(42, 7, state);

            object variables = apiConnection.VariablesByQuery[AiQueries.saveAiSessionState];
            Assert.Multiple(() =>
            {
                Assert.That(saved, Is.True);
                Assert.That(GetVariable<long>(variables, "id"), Is.EqualTo(42L));
                Assert.That(GetVariable<int>(variables, "userId"), Is.EqualTo(7));
                Assert.That(GetVariable<string>(variables, "state"), Is.EqualTo(state));
            });
        }

        private static T GetVariable<T>(object variables, string name)
        {
            object? value = variables.GetType().GetProperty(name)?.GetValue(variables);
            Assert.That(value, Is.Not.Null);
            return (T)value!;
        }

        private sealed class CapturingApiConnection : ApiConnection
        {
            public Dictionary<string, object> VariablesByQuery { get; } = [];
            public string ConfigResponseJson { get; set; } = "";
            public string ProviderResponseJson { get; set; } = "";

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                if (variables != null)
                {
                    VariablesByQuery[query] = variables;
                }
                if (query == AiQueries.getAiConfig && !string.IsNullOrWhiteSpace(ConfigResponseJson))
                {
                    return Task.FromResult(JsonConvert.DeserializeObject<QueryResponseType>(ConfigResponseJson)!);
                }
                if (query == AiQueries.getAiProviders && !string.IsNullOrWhiteSpace(ProviderResponseJson))
                {
                    return Task.FromResult(JsonConvert.DeserializeObject<QueryResponseType>(ProviderResponseJson)!);
                }
                if (query == AiQueries.saveAiSessionState && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    ReturnIdWrapper wrapper = new() { ReturnIds = [new ReturnId { UpdatedIdLong = GetVariable<long>(variables!, "id") }] };
                    return Task.FromResult((QueryResponseType)(object)wrapper);
                }
                if (query == AiQueries.addAiSession && typeof(QueryResponseType) == typeof(ReturnIdWrapper))
                {
                    ReturnIdWrapper wrapper = new() { ReturnIds = [new ReturnId { NewIdLong = 42 }] };
                    return Task.FromResult((QueryResponseType)(object)wrapper);
                }
                if (query == AiQueries.getAiSessionById && typeof(QueryResponseType) == typeof(List<AiSession>))
                {
                    object addVariables = VariablesByQuery[AiQueries.addAiSession];
                    List<AiSession> sessions =
                    [
                        new()
                        {
                            Id = 42,
                            UserId = 1,
                            Name = "Session",
                            ModelId = GetVariable<string>(addVariables, "modelId")
                        }
                    ];
                    return Task.FromResult((QueryResponseType)(object)sessions);
                }
                return Task.FromResult(Activator.CreateInstance<QueryResponseType>());
            }

            public override void SetAuthHeader(string jwt) { }
            public override void SetRole(string role) { }
            public override void SetBestRole(System.Security.Claims.ClaimsPrincipal user, List<string> targetRoleList) { }
            public override void SwitchBack() { }
            public override Task ReconnectSubscriptionsAsync(string jwt, CancellationToken ct) => Task.CompletedTask;
            public override Task<ApiResponse<QueryResponseType>> SendQuerySafeAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            public override GraphQlApiSubscription<SubscriptionResponseType> GetSubscription<SubscriptionResponseType>(Action<Exception> exceptionHandler, GraphQlApiSubscription<SubscriptionResponseType>.SubscriptionUpdate subscriptionUpdateHandler, string subscription, object? variables = null, string? operationName = null) => throw new NotImplementedException();
            public override void DisposeSubscriptions<T>() { }
            protected override void Dispose(bool disposing) { }
        }
    }
}
