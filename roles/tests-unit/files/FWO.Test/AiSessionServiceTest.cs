using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Ai;
using FWO.Middleware.Server.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AiSessionServiceTest
    {
        [Test]
        public void CreateSession_ThrowsWhenNoEnabledModelIsConfigured()
        {
            CapturingApiConnection apiConnection = new() { ConfigResponseJson = "[]", ProviderResponseJson = "[]" };
            AiSessionService sessionService = new(apiConnection, new AiSettingsService(apiConnection));

            Assert.Multiple(() =>
            {
                Assert.ThrowsAsync<InvalidOperationException>(() => sessionService.CreateSession(1, "session", 0, null));
                Assert.That(apiConnection.Queries, Does.Not.Contain(AiQueries.addAiSession));
            });
        }

        [Test]
        public async Task CreateSession_PersistsResolvedProviderAndModel()
        {
            CapturingApiConnection apiConnection = new()
            {
                ConfigResponseJson = "[]",
                ProviderResponseJson = """
                [
                  {
                    "id": 3,
                    "kind": "Ollama",
                    "display_name": "Ollama",
                    "enabled": true,
                    "models": [ { "model_id": "llama", "enabled": true } ]
                  }
                ]
                """
            };
            AiSessionService sessionService = new(apiConnection, new AiSettingsService(apiConnection));

            AiSession session = await sessionService.CreateSession(1, null, 0, null);

            object variables = apiConnection.VariablesByQuery[AiQueries.addAiSession];
            Assert.Multiple(() =>
            {
                Assert.That(GetVariable<long>(variables, "providerId"), Is.EqualTo(3L));
                Assert.That(GetVariable<string>(variables, "modelId"), Is.EqualTo("llama"));
                Assert.That(session.ProviderId, Is.EqualTo(3L));
                Assert.That(session.ModelId, Is.EqualTo("llama"));
            });
        }

        [Test]
        public async Task SaveState_ScopesMutationToUser()
        {
            CapturingApiConnection apiConnection = new();
            AiSessionService sessionService = new(apiConnection, new AiSettingsService(apiConnection));
            JObject state = JObject.Parse("""{"messages":[]}""");

            bool saved = await sessionService.SaveState(42, 7, state);

            object variables = apiConnection.VariablesByQuery[AiQueries.saveAiSessionState];
            Assert.Multiple(() =>
            {
                Assert.That(saved, Is.True);
                Assert.That(GetVariable<long>(variables, "id"), Is.EqualTo(42L));
                Assert.That(GetVariable<int>(variables, "userId"), Is.EqualTo(7));
                Assert.That(GetVariable<JToken>(variables, "state"), Is.SameAs(state));
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
            public List<string> Queries { get; } = [];
            public Dictionary<string, object> VariablesByQuery { get; } = [];
            public string ConfigResponseJson { get; set; } = "";
            public string ProviderResponseJson { get; set; } = "";

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                Queries.Add(query);
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
                    ReturnIdWrapper wrapper = new()
                    {
                        ReturnIds = [new ReturnId { UpdatedIdLong = GetVariable<long>(variables!, "id") }]
                    };
                    return Task.FromResult((QueryResponseType)(object)wrapper);
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
