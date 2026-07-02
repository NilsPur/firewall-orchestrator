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
            string originalKey = InvokeCacheKey(provider, model);

            provider.ApiKeyEnvVariable = "FWO_AI_SECOND_KEY";
            string changedKey = InvokeCacheKey(provider, model);

            Assert.That(changedKey, Is.Not.EqualTo(originalKey));
        }

        [Test]
        public void CacheKey_ChangesWithToolCallsSupported()
        {
            AiProviderConfig provider = BuildProvider();
            AiModelConfig model = BuildModel();
            string originalKey = InvokeCacheKey(provider, model);

            model.ToolCallsSupported = false;
            string changedKey = InvokeCacheKey(provider, model);

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

        private sealed class DummyTool : AITool { }

        private static AiProviderConfig BuildProvider()
        {
            return new AiProviderConfig
            {
                Id = 1,
                Kind = AiProviderKind.OpenAiCompatible,
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

        private static string InvokeCacheKey(AiProviderConfig provider, AiModelConfig model)
        {
            MethodInfo method = typeof(AgentFactoryService).GetMethod("CacheKey", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(nameof(AgentFactoryService), "CacheKey");
            return (string)method.Invoke(null, [provider, model])!;
        }
    }
}
