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
        public void Defaults_HaveNoProvidersAndDefaultPrompt()
        {
            AiSettings settings = new();

            Assert.Multiple(() =>
            {
                Assert.That(settings.Providers, Is.Empty);
                Assert.That(settings.SystemPrompt, Is.EqualTo(AiSettingsDefaults.SystemPrompt));
                Assert.That(settings.SystemPrompt, Does.Contain("tools instead of assumptions"));
                Assert.That(settings.SystemPrompt, Does.Contain("tool data is missing or insufficient"));
                Assert.That(settings.InitialModelId, Is.Empty);
            });
        }

        [Test]
        public void GenerationParameters_AreNullByDefault()
        {
            AiModelConfig model = new();

            Assert.Multiple(() =>
            {
                Assert.That(model.Temperature, Is.Null);
                Assert.That(model.TopP, Is.Null);
                Assert.That(model.TopK, Is.Null);
                Assert.That(model.MaxOutputTokens, Is.Null);
                Assert.That(model.ReasoningEffort, Is.Null);
                Assert.That(model.StreamingSupported, Is.Null);
                Assert.That(model.ToolCallsSupported, Is.Null);
            });
        }

        [Test]
        public void Model_SerializesSupportedFlags()
        {
            AiModelConfig model = new() { ModelId = "m", StreamingSupported = true, ToolCallsSupported = false, VisionSupported = true, ReasoningSupported = false };

            string serialized = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(serialized, Does.Contain("\"streaming_supported\":true"));
                Assert.That(serialized, Does.Contain("\"tool_calls_supported\":false"));
                Assert.That(serialized, Does.Not.Contain("capabilities"));
            });
        }

        [Test]
        public void ResolveModelSelection_UsesRequestedProviderAndModel()
        {
            AiSettings settings = new()
            {
                Providers =
                [
                    new AiProviderConfig { Id = 1, Enabled = true, Models = [new AiModelConfig { ModelId = "shared", Enabled = true }] },
                    new AiProviderConfig { Id = 2, Enabled = true, Models = [new AiModelConfig { ModelId = "shared", Enabled = true }] }
                ]
            };

            (AiProviderConfig Provider, AiModelConfig Model)? selection = AiSettingsService.ResolveModelSelection(settings, 2, "shared");

            Assert.Multiple(() =>
            {
                Assert.That(selection?.Provider.Id, Is.EqualTo(2));
                Assert.That(selection?.Model.ModelId, Is.EqualTo("shared"));
            });
        }

        [Test]
        public void ResolveModelSelection_FallsBackToInitialSelection()
        {
            AiSettings settings = new()
            {
                InitialModelId = AiModelConfig.BuildSelectionId(1, "init"),
                Providers =
                [
                    new AiProviderConfig { Id = 1, Enabled = true, Models = [new AiModelConfig { ModelId = "init", Enabled = true }] }
                ]
            };

            (AiProviderConfig Provider, AiModelConfig Model)? selection = AiSettingsService.ResolveModelSelection(settings, 2, "missing");

            Assert.Multiple(() =>
            {
                Assert.That(selection?.Provider.Id, Is.EqualTo(1));
                Assert.That(selection?.Model.ModelId, Is.EqualTo("init"));
            });
        }

        [Test]
        public void ResolveModelSelection_ReturnsNullWhenNoModelIsEnabled()
        {
            Assert.That(AiSettingsService.ResolveModelSelection(new AiSettings(), 1, "missing"), Is.Null);
        }

        [Test]
        public void AssistantSettings_ContainsOnlyEnabledModels()
        {
            AiSettings settings = new()
            {
                InitialModelId = "disabled-model",
                Providers =
                [
                    new AiProviderConfig
                    {
                        Id = 1,
                        Enabled = true,
                        Models =
                        [
                            new AiModelConfig { ModelId = "enabled-model", DisplayName = "Enabled", Enabled = true },
                            new AiModelConfig { ModelId = "disabled-model", DisplayName = "Disabled", Enabled = false }
                        ]
                    },
                    new AiProviderConfig
                    {
                        Id = 2,
                        Enabled = false,
                        Models =
                        [
                            new AiModelConfig { ModelId = "hidden-model", DisplayName = "Hidden", Enabled = true }
                        ]
                    }
                ]
            };

            AiAssistantSettings assistantSettings = AiSettingsService.CreateAssistantSettings(settings);

            Assert.Multiple(() =>
            {
                Assert.That(assistantSettings.InitialModelId, Is.Empty);
                Assert.That(assistantSettings.EnabledModels.Select(model => model.ModelId), Is.EqualTo(new[] { "enabled-model" }));
                Assert.That(assistantSettings.EnabledModels.Single().ProviderId, Is.EqualTo(1));
            });
        }

        [Test]
        public void AssistantSettings_KeepsValidInitialModel()
        {
            AiSettings settings = new()
            {
                InitialModelId = AiModelConfig.BuildSelectionId(7, "enabled-model"),
                Providers =
                [
                    new AiProviderConfig
                    {
                        Id = 7,
                        Enabled = true,
                        Models = [new AiModelConfig { ModelId = "enabled-model", DisplayName = "Enabled", Enabled = true }]
                    }
                ]
            };

            AiAssistantSettings assistantSettings = AiSettingsService.CreateAssistantSettings(settings);

            Assert.That(assistantSettings.InitialModelId, Is.EqualTo(AiModelConfig.BuildSelectionId(7, "enabled-model")));
        }

        [Test]
        public void AssistantSettings_AcceptsLegacyInitialModelId()
        {
            AiSettings settings = new()
            {
                InitialModelId = "enabled-model",
                Providers =
                [
                    new AiProviderConfig
                    {
                        Id = 7,
                        Enabled = true,
                        Models = [new AiModelConfig { ModelId = "enabled-model", DisplayName = "Enabled", Enabled = true }]
                    }
                ]
            };

            AiAssistantSettings assistantSettings = AiSettingsService.CreateAssistantSettings(settings);

            Assert.That(assistantSettings.InitialModelId, Is.EqualTo(AiModelConfig.BuildSelectionId(7, "enabled-model")));
        }

        [Test]
        public async Task SaveSettings_UpsertsKeptProvidersAndOmitsIdForNewOnes()
        {
            CapturingApiConnection apiConnection = new();
            AiSettingsService settingsService = new(apiConnection);
            AiSettings settings = new()
            {
                SystemPrompt = "prompt",
                InitialModelId = "llama",
                Providers =
                [
                    new AiProviderConfig { Id = 7, Kind = AiProviderKind.Ollama, Enabled = true, Models = [new AiModelConfig { ModelId = "llama", Enabled = true, StreamingSupported = true }] },
                    new AiProviderConfig { Id = 0, Kind = AiProviderKind.OpenAi }
                ]
            };

            await settingsService.SaveSettings(settings);

            List<long> keepProviderIds = GetVariable<List<long>>(apiConnection.LastVariables!, "keepProviderIds");
            List<FWO.Config.Api.Data.ConfigItem> configItems = GetVariable<List<FWO.Config.Api.Data.ConfigItem>>(apiConnection.LastVariables!, "configItems");
            List<object> providers = [.. GetVariable<System.Collections.IEnumerable>(apiConnection.LastVariables!, "providers").Cast<object>()];
            Dictionary<string, object?> keptProvider = (Dictionary<string, object?>)providers[0];
            Dictionary<string, object?> newProvider = (Dictionary<string, object?>)providers[1];
            List<object> models = [.. GetProperty<System.Collections.IEnumerable>(keptProvider["models"]!, "data").Cast<object>()];
            object keptModel = models.Single();

            Assert.Multiple(() =>
            {
                Assert.That(apiConnection.LastQuery, Is.EqualTo(AiQueries.saveAiSettings));
                Assert.That(keepProviderIds, Is.EqualTo(new long[] { 7 }));
                Assert.That(configItems.Single(item => item.Key == "system_prompt").Value, Is.EqualTo("prompt"));
                Assert.That(configItems.Single(item => item.Key == "aiLastModelId").Value, Is.EqualTo("llama"));
                Assert.That(configItems.All(item => item.User == 0), Is.True);
                Assert.That(keptProvider.ContainsKey("id"), Is.True);
                Assert.That(keptProvider["id"], Is.EqualTo(7L));
                Assert.That(newProvider.ContainsKey("id"), Is.False);
                Assert.That(GetProperty<bool?>(keptModel, "streaming_supported"), Is.True);
            });
        }

        [Test]
        public async Task GetSettings_ReadsPromptAndLastModelFromGlobalConfig()
        {
            CapturingApiConnection apiConnection = new()
            {
                ConfigResponseJson = """
                [
                  { "config_key": "system_prompt", "config_value": "stored prompt" },
                  { "config_key": "aiLastModelId", "config_value": "stored-model" }
                ]
                """,
                ProviderResponseJson = "[]"
            };
            AiSettingsService settingsService = new(apiConnection);

            AiSettings settings = await settingsService.GetSettings();

            Assert.Multiple(() =>
            {
                Assert.That(settings.SystemPrompt, Is.EqualTo("stored prompt"));
                Assert.That(settings.InitialModelId, Is.EqualTo("stored-model"));
            });
        }

        [Test]
        public async Task GetSettings_ReadsSeededProviders()
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
                        "model_id": "qwen3:5.2b",
                        "display_name": "qwen3:5.2b",
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
                Assert.That(apiConnection.Queries, Does.Contain(AiQueries.getAiConfig));
                Assert.That(apiConnection.Queries, Does.Contain(AiQueries.getAiProviders));
                Assert.That(settings.Providers, Has.Count.EqualTo(1));
                Assert.That(settings.Providers[0].Kind, Is.EqualTo(AiProviderKind.Ollama));
                Assert.That(settings.Providers[0].Models[0].ProviderId, Is.EqualTo(1));
                Assert.That(settings.Providers[0].Models[0].ProviderDisplayName, Is.EqualTo("Ollama"));
                Assert.That(settings.Providers[0].Models[0].ToolCallsSupported, Is.True);
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
            public List<string> Queries { get; } = [];
            public string ConfigResponseJson { get; set; } = "";
            public string ProviderResponseJson { get; set; } = "";

            public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null, string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
            {
                LastQuery = query;
                Queries.Add(query);
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
