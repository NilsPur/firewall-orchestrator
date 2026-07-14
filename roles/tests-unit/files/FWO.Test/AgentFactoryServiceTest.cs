using FWO.Data.Ai;
using FWO.Middleware.Server.Services;
using Microsoft.Extensions.AI;
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test
{
    [TestFixture]
    public class AgentFactoryServiceTest
    {
        [Test]
        public void CacheKey_ChangesWithApiKeyEnvVariable()
        {
            AiProviderConfig provider = BuildProvider();
            AiModelConfig model = BuildModel();
            string originalKey = InvokeCacheKey(provider, model, "prompt");

            provider.ApiKeyEnvVariable = "FWO_AI_SECOND_KEY";
            string changedKey = InvokeCacheKey(provider, model, "prompt");

            Assert.That(changedKey, Is.Not.EqualTo(originalKey));
        }

        [Test]
        public void CacheKey_ChangesWithProviderKind()
        {
            AiProviderConfig provider = BuildProvider();
            AiModelConfig model = BuildModel();
            string originalKey = InvokeCacheKey(provider, model, "prompt");

            provider.Kind = AiProviderKind.Ollama;
            string changedKey = InvokeCacheKey(provider, model, "prompt");

            Assert.That(changedKey, Is.Not.EqualTo(originalKey));
        }

        [Test]
        public void CacheKey_ChangesWithToolCallsSupported()
        {
            AiProviderConfig provider = BuildProvider();
            AiModelConfig model = BuildModel();
            string originalKey = InvokeCacheKey(provider, model, "prompt");

            model.ToolCallsSupported = false;
            string changedKey = InvokeCacheKey(provider, model, "prompt");

            Assert.That(changedKey, Is.Not.EqualTo(originalKey));
        }

        [Test]
        public void CacheKey_ChangesWithSystemPrompt()
        {
            AiProviderConfig provider = BuildProvider();
            AiModelConfig model = BuildModel();
            string originalKey = InvokeCacheKey(provider, model, "first prompt");

            string changedKey = InvokeCacheKey(provider, model, "second prompt");

            Assert.That(changedKey, Is.Not.EqualTo(originalKey));
        }

        [Test]
        public void BuildChatOptions_OmitsToolsWhenToolCallsDisabled()
        {
            List<AITool> tools = [new DummyTool()];

            ChatOptions options = AgentFactoryService.BuildChatOptions(new AiModelConfig { ToolCallsSupported = false }, tools);

            Assert.Multiple(() =>
            {
                Assert.That(options.Tools, Is.Null);
                Assert.That(options.ToolMode, Is.Null);
            });
        }

        [Test]
        public void BuildChatOptions_AttachesToolsOnlyWhenEnabled()
        {
            List<AITool> tools = [new DummyTool()];

            ChatOptions unspecifiedOptions = AgentFactoryService.BuildChatOptions(new AiModelConfig(), tools);
            ChatOptions supportedOptions = AgentFactoryService.BuildChatOptions(new AiModelConfig { ToolCallsSupported = true }, tools);

            Assert.Multiple(() =>
            {
                Assert.That(unspecifiedOptions.Tools, Is.Null);
                Assert.That(unspecifiedOptions.ToolMode, Is.Null);
                Assert.That(supportedOptions.Tools, Is.EqualTo(tools));
                Assert.That(supportedOptions.ToolMode, Is.Not.Null);
            });
        }

        [Test]
        public void BuildAgent_DoesNotResolveOpenAiApiKey()
        {
            AiProviderConfig provider = BuildProvider();
            provider.ApiKeyEnvVariable = $"FWO_AI_TEST_KEY_{Guid.NewGuid():N}";
            Environment.SetEnvironmentVariable(provider.ApiKeyEnvVariable, null);
            AiModelConfig model = BuildModel();
            AgentFactoryService factory = new(null!, null!);

            Assert.DoesNotThrow(() => factory.BuildAgent(provider, model, null));
        }

        private sealed class DummyTool : AITool { }

        private static AiProviderConfig BuildProvider()
        {
            return new AiProviderConfig
            {
                Id = 1,
                Kind = AiProviderKind.OpenAi,
                EndpointUrl = "https://example.invalid/v1",
                ApiKeyEnvVariable = "FWO_AI_FIRST_KEY",
                TimeoutSeconds = 60,
                MaxRetries = 1
            };
        }

        private static AiModelConfig BuildModel()
        {
            return new AiModelConfig
            {
                ModelId = "model",
                Temperature = 0.1F,
                TopP = 0.2F,
                TopK = 3,
                MaxOutputTokens = 4,
                ReasoningEffort = FWO.Data.Ai.ReasoningEffort.Low
            };
        }

        private static string InvokeCacheKey(AiProviderConfig provider, AiModelConfig model, string systemPrompt)
        {
            MethodInfo method = typeof(AgentFactoryService).GetMethod("CacheKey", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(nameof(AgentFactoryService), "CacheKey");
            return (string)method.Invoke(null, [provider, model, systemPrompt])!;
        }
    }
}
