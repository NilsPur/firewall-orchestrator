using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Data.Ai;
using FWO.Middleware.Server.Services;
using Newtonsoft.Json;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AiSettingsDefaultsTest
    {
        [Test]
        public void Defaults_UseSingleOpenAiProviderAndModel()
        {
            AiSettings settings = new();

            Assert.Multiple(() =>
            {
                Assert.That(settings.SystemPrompt, Is.EqualTo(AiSettingsDefaults.SystemPrompt));
                Assert.That(settings.Provider.Id, Is.EqualTo(1));
                Assert.That(settings.Provider.Kind, Is.EqualTo(AiProviderKind.OpenAi));
                Assert.That(settings.Provider.ApiKeyEnvVariable, Is.EqualTo("OPENAI_API_KEY"));
                Assert.That(settings.Model.ModelId, Is.EqualTo(AiSettingsDefaults.ModelId));
                Assert.That(settings.Model.ToolCallsSupported, Is.True);
                Assert.That(settings.Model.ReasoningSupported, Is.True);
            });
        }

        [Test]
        public void ProviderKind_SupportsAllExistingKinds()
        {
            Assert.That(Enum.GetValues<AiProviderKind>(), Is.EquivalentTo(new[]
            {
                AiProviderKind.Ollama,
                AiProviderKind.OpenAi,
                AiProviderKind.Anthropic,
                AiProviderKind.Google,
                AiProviderKind.OpenAiCompatible
            }));
        }

        [Test]
        public void ResolveModel_ReturnsSingleEnabledModel()
        {
            (AiProviderConfig Provider, AiModelConfig Model)? selection = AiSettingsService.ResolveModel(new AiSettings());

            Assert.Multiple(() =>
            {
                Assert.That(selection?.Provider.Kind, Is.EqualTo(AiProviderKind.OpenAi));
                Assert.That(selection?.Model.ModelId, Is.EqualTo(AiSettingsDefaults.ModelId));
            });
        }

        [Test]
        public void AssistantSettings_ReturnsSingleModel()
        {
            AiAssistantSettings assistantSettings = AiSettingsService.CreateAssistantSettings(new AiSettings());

            Assert.That(assistantSettings.Model.ModelId, Is.EqualTo(AiSettingsDefaults.ModelId));
        }

        [Test]
        public async Task SaveSettings_UpsertsSingleProviderAndModel()
        {
            CapturingApiConnection apiConnection = new();
            AiSettingsService settingsService = new(apiConnection);
            AiSettings settings = new() { SystemPrompt = "prompt" };

            await settingsService.SaveSettings(settings);

            List<FWO.Config.Api.Data.ConfigItem> configItems = GetVariable<List<FWO.Config.Api.Data.ConfigItem>>(apiConnection.LastVariables!, "configItems");
            Dictionary<string, object?> provider = GetVariable<Dictionary<string, object?>>(apiConnection.LastVariables!, "provider");
            object model = GetVariable<object>(apiConnection.LastVariables!, "model");

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(AiQueries.saveAiSettings));
                Assert.That(configItems.Single().Key, Is.EqualTo("system_prompt"));
                Assert.That(configItems.Single().Value, Is.EqualTo("prompt"));
                Assert.That(provider["id"], Is.EqualTo(1L));
                Assert.That(provider["kind"], Is.EqualTo("OpenAi"));
                Assert.That(GetProperty<string>(model, "model_id"), Is.EqualTo(AiSettingsDefaults.ModelId));
                Assert.That(GetProperty<long>(model, "provider_id"), Is.EqualTo(1L));
                Assert.That(GetProperty<bool?>(model, "tool_calls_supported"), Is.True);
            });
        }

        [Test]
        public async Task SaveSettings_PreservesAdminSelectedProviderKind()
        {
            CapturingApiConnection apiConnection = new();
            AiSettingsService settingsService = new(apiConnection);
            AiSettings settings = new()
            {
                Provider = new AiProviderConfig { Kind = AiProviderKind.Ollama, EndpointUrl = "http://127.0.0.1:11434" },
                Model = new AiModelConfig { ModelId = "qwen3.5:4b" }
            };

            AiSettings saved = await settingsService.SaveSettings(settings);

            Dictionary<string, object?> provider = GetVariable<Dictionary<string, object?>>(apiConnection.LastVariables!, "provider");

            Assert.Multiple(() =>
            {
                Assert.That(saved.Provider.Kind, Is.EqualTo(AiProviderKind.Ollama));
                Assert.That(provider["kind"], Is.EqualTo("Ollama"));
            });
        }

        [Test]
        public async Task SaveSettings_PreservesEmptyApiKeyEnvironmentVariableForOllama()
        {
            CapturingApiConnection apiConnection = new();
            AiSettingsService settingsService = new(apiConnection);
            AiSettings settings = new()
            {
                Provider = new AiProviderConfig
                {
                    Kind = AiProviderKind.Ollama,
                    EndpointUrl = "http://127.0.0.1:11434",
                    ApiKeyEnvVariable = ""
                },
                Model = new AiModelConfig { ModelId = "qwen3.5:4b" }
            };

            AiSettings saved = await settingsService.SaveSettings(settings);

            Dictionary<string, object?> provider = GetVariable<Dictionary<string, object?>>(apiConnection.LastVariables!, "provider");
            Assert.Multiple(() =>
            {
                Assert.That(saved.Provider.ApiKeyEnvVariable, Is.Empty);
                Assert.That(provider["api_key_env_variable"], Is.EqualTo(""));
            });
        }

        [Test]
        public async Task GetSettings_PreservesNonDefaultSeededProviderKind()
        {
            CapturingApiConnection apiConnection = new()
            {
                ConfigResponseJson = "[]",
                ProviderResponseJson = """
                [
                  {
                    "id": 1,
                    "kind": "Ollama",
                    "display_name": "Ollama",
                    "endpoint_url": "http://127.0.0.1:11434",
                    "enabled": true,
                    "models": [
                      {
                        "model_id": "qwen3.5:4b",
                        "display_name": "qwen3.5:4b",
                        "enabled": true
                      }
                    ]
                  }
                ]
                """
            };
            AiSettingsService settingsService = new(apiConnection);

            AiSettings settings = await settingsService.GetSettings();

            Assert.Multiple(() =>
            {
                Assert.That(settings.Provider.Kind, Is.EqualTo(AiProviderKind.Ollama));
                Assert.That(settings.Provider.ApiKeyEnvVariable, Is.Empty);
            });
        }

        [Test]
        public async Task GetSettings_ReadsSeededProviderAndModel()
        {
            CapturingApiConnection apiConnection = new()
            {
                ConfigResponseJson = """[{ "config_key": "system_prompt", "config_value": "stored prompt" }]""",
                ProviderResponseJson = """
                [
                  {
                    "id": 1,
                    "kind": "OpenAi",
                    "display_name": "OpenAI",
                    "api_key_env_variable": "OPENAI_API_KEY",
                    "enabled": true,
                    "models": [
                      {
                        "model_id": "gpt-5.4-mini",
                        "display_name": "GPT-5.4 mini",
                        "enabled": true,
                        "tool_calls_supported": true
                      }
                    ]
                  }
                ]
                """
            };
            AiSettingsService settingsService = new(apiConnection);

            AiSettings settings = await settingsService.GetSettings();

            Assert.Multiple(() =>
            {
                Assert.That(settings.SystemPrompt, Is.EqualTo("stored prompt"));
                Assert.That(settings.Provider.Kind, Is.EqualTo(AiProviderKind.OpenAi));
                Assert.That(settings.Model.ProviderId, Is.EqualTo(1));
                Assert.That(settings.Model.ProviderDisplayName, Is.EqualTo("OpenAI"));
                Assert.That(settings.Model.ToolCallsSupported, Is.True);
            });
        }

        private static T GetVariable<T>(object variables, string name)
        {
            object? value = variables.GetType().GetProperty(name)?.GetValue(variables);
            Assert.That(value, Is.Not.Null);
            return (T)value!;
        }

        private static T GetProperty<T>(object source, string name)
        {
            object? value = source.GetType().GetProperty(name)?.GetValue(source);
            Assert.That(value, Is.Not.Null);
            return (T)value!;
        }

        private sealed class CapturingApiConnection : ApiConnection
        {
            public string LastQuery { get; private set; } = "";
            public object? LastVariables { get; private set; }
            public string ConfigResponseJson { get; set; } = "";
            public string ProviderResponseJson { get; set; } = "";

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                LastVariables = variables;
                if (query == AiQueries.getAiConfig && !string.IsNullOrWhiteSpace(ConfigResponseJson))
                {
                    return Task.FromResult(JsonConvert.DeserializeObject<QueryResponseType>(ConfigResponseJson)!);
                }
                if (query == AiQueries.getAiProviders && !string.IsNullOrWhiteSpace(ProviderResponseJson))
                {
                    return Task.FromResult(JsonConvert.DeserializeObject<QueryResponseType>(ProviderResponseJson)!);
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
