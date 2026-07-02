using FWO.Data.Ai;
using FWO.Middleware.Server.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class AiOllamaModelServiceTest
    {
        [Test]
        public void BuildModelsResponse_ReturnsSeparateDownloadAndDownloadedLists()
        {
            List<AiOllamaModelState> downloadedModels =
            [
                new() { ModelId = "llama3.2", DownloadStatus = AiOllamaModelState.kStatusDownloaded, DownloadProgress = 100 }
            ];
            List<AiOllamaModelState> pullStates =
            [
                new() { ModelId = "mistral", DownloadStatus = AiOllamaModelState.kStatusDownloading, DownloadProgress = 42 },
                new() { ModelId = "gemma", DownloadStatus = AiOllamaModelState.kStatusFailed, DownloadProgress = 0 }
            ];

            AiOllamaModelsResponse models = AiOllamaModelService.BuildModelsResponse(downloadedModels, pullStates);

            Assert.Multiple(() =>
            {
                Assert.That(models.Downloads.Select(model => model.ModelId), Is.EqualTo(new[] { "gemma", "mistral" }));
                Assert.That(models.Downloads.Single(model => model.ModelId == "mistral").DownloadStatus, Is.EqualTo(AiOllamaModelState.kStatusDownloading));
                Assert.That(models.Downloads.Single(model => model.ModelId == "gemma").DownloadStatus, Is.EqualTo(AiOllamaModelState.kStatusFailed));
                Assert.That(models.DownloadedModels.Select(model => model.ModelId), Is.EqualTo(new[] { "llama3.2" }));
                Assert.That(models.DownloadedModels.Single().DownloadStatus, Is.EqualTo(AiOllamaModelState.kStatusDownloaded));
            });
        }

        [Test]
        public void BuildModelsResponse_KeepsDownloadedModelsSeparateFromDownloads()
        {
            List<AiOllamaModelState> downloadedModels =
            [
                new() { ModelId = "llama3.2", DownloadStatus = AiOllamaModelState.kStatusDownloaded, DownloadProgress = 100 }
            ];
            List<AiOllamaModelState> pullStates =
            [
                new() { ModelId = "llama3.2", DownloadStatus = AiOllamaModelState.kStatusDownloading, DownloadProgress = 10 }
            ];

            AiOllamaModelsResponse models = AiOllamaModelService.BuildModelsResponse(downloadedModels, pullStates);

            Assert.Multiple(() =>
            {
                Assert.That(models.Downloads, Has.Count.EqualTo(1));
                Assert.That(models.DownloadedModels, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public void GetEnabledOllamaModelIds_ReturnsOnlyEnabledConfiguredModels()
        {
            AiProviderConfig ollamaProvider = new()
            {
                Models =
                [
                    new() { ModelId = "llama3.2", Enabled = true },
                    new() { ModelId = "disabled-model", Enabled = false },
                    new() { ModelId = "", Enabled = true },
                    new() { ModelId = "mistral", Enabled = true }
                ]
            };

            IEnumerable<string> modelIds = AiOllamaModelService.GetEnabledOllamaModelIds(ollamaProvider);

            Assert.That(modelIds, Is.EqualTo(new[] { "llama3.2", "mistral" }));
        }
    }
}
