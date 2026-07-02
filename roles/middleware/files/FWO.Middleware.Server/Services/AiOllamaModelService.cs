using FWO.Data.Ai;
using FWO.Data.Middleware;
using FWO.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using System.Collections.Concurrent;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Manages local Ollama models and download progress through the OllamaSharp client.
    /// </summary>
    public class AiOllamaModelService(AiSettingsService settingsService) : BackgroundService
    {
        private readonly AiSettingsService settingsService = settingsService;
        private readonly ConcurrentDictionary<string, AiOllamaModelState> pullStates = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> pullCancellations = new();

        /// <summary>
        /// Executes the startup model warmup loop once.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Get Ollama provider
                AiProviderConfig? ollamaProvider = FindOllamaProvider(await settingsService.GetSettings());
                if (ollamaProvider == null || !ollamaProvider.Enabled)
                {
                    return;
                }

                using OllamaApiClient ollamaClient = new(ollamaProvider.EndpointUrl);
                foreach (string modelId in GetEnabledOllamaModelIds(ollamaProvider))
                {
                    try
                    {
                        // Generate dummy request with keep alive set to infinite time.
                        await foreach (var _ in ollamaClient.GenerateAsync(new GenerateRequest
                        {
                            Model = modelId,
                            Prompt = "",
                            Stream = false,
                            KeepAlive = "-1"
                        }, stoppingToken)) { /* Just warmup */ }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        Log.WriteWarning("AI Ollama warmup", $"{modelId}: {BuildRequestErrorMessage(ollamaProvider.EndpointUrl, exception)}");
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteWarning("AI Ollama warmup", exception.Message);
            }
        }

        /// <summary>
        /// Lists downloaded Ollama models and retained download progress separately.
        /// Returns only the download state when the Ollama provider is disabled or unreachable.
        /// </summary>
        public async Task<AiOllamaModelsResponse> GetModels()
        {
            AiProviderConfig? ollamaProvider = FindOllamaProvider(await settingsService.GetSettings());
            if (ollamaProvider == null || !ollamaProvider.Enabled)
            {
                return BuildModelsResponse([], pullStates.Values);
            }
            try
            {
                using OllamaApiClient ollamaClient = new(ollamaProvider.EndpointUrl);
                List<Model> availableModels = (await ollamaClient.ListLocalModelsAsync()).ToList();
                List<AiOllamaModelState> downloadedModels = availableModels
                    .Where(model => !string.IsNullOrWhiteSpace(model.ModelName))
                    .Select(model => new AiOllamaModelState
                    {
                        ModelId = model.ModelName!,
                        DownloadStatus = AiOllamaModelState.kStatusDownloaded,
                        DownloadProgress = 100
                    })
                    .ToList();
                return BuildModelsResponse(downloadedModels, pullStates.Values);
            }
            catch (Exception exception)
            {
                Log.WriteWarning("AI Ollama models", BuildRequestErrorMessage(ollamaProvider.EndpointUrl, exception));
                return BuildModelsResponse([], pullStates.Values);
            }
        }

        /// <summary>
        /// Starts a model download if no download is already running for the model.
        /// </summary>
        public AiOperationResult DownloadModel(string modelId)
        {
            modelId = modelId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(modelId))
            {
                return new AiOperationResult { Success = false, MessageKey = "ai_model_id_required" };
            }
            CancellationTokenSource cts = new();
            if (!pullCancellations.TryAdd(modelId, cts))
            {
                cts.Dispose();
                return new AiOperationResult { Success = true, MessageKey = "ai_download_running", MessageArgument = modelId };
            }
            pullStates[modelId] = new AiOllamaModelState { ModelId = modelId, DownloadStatus = AiOllamaModelState.kStatusDownloading, DownloadProgress = 0 };
            _ = Task.Run(() => PullModelInBackground(modelId, cts));
            return new AiOperationResult { Success = true, MessageKey = "ai_download_queued", MessageArgument = modelId };
        }

        /// <summary>
        /// Cancels a model download.
        /// </summary>
        public AiOperationResult CancelDownloadModel(string modelId)
        {
            modelId = modelId?.Trim() ?? "";
            if (pullCancellations.TryRemove(modelId, out CancellationTokenSource? cts))
            {
                try
                {
                    cts.Cancel();
                    return new AiOperationResult { Success = true, MessageKey = "ai_download_cancelled", MessageArgument = modelId };
                }
                catch (ObjectDisposedException)
                {
                    // The background pull finished and disposed the token between TryGetValue and Cancel.
                }
            }
            return new AiOperationResult { Success = false, MessageKey = "ai_download_none", MessageArgument = modelId };
        }

        /// <summary>
        /// Deletes a downloaded Ollama model.
        /// </summary>
        public async Task<AiOperationResult> DeleteModel(string modelId)
        {
            modelId = modelId?.Trim() ?? "";
            AiProviderConfig? provider = null;
            try
            {
                provider = GetOllamaProvider(await settingsService.GetSettings());
                using OllamaApiClient client = new(provider.EndpointUrl);
                await client.DeleteModelAsync(new DeleteModelRequest { Model = modelId });
                return new AiOperationResult { Success = true, MessageKey = "ai_model_deleted", MessageArgument = modelId };
            }
            catch (Exception exception)
            {
                return new AiOperationResult
                {
                    Success = false,
                    MessageKey = "ai_model_delete_failed",
                    MessageArgument = BuildRequestErrorMessage(provider?.EndpointUrl ?? "", exception)
                };
            }
        }

        private async Task PullModelInBackground(string modelId, CancellationTokenSource cts)
        {
            AiProviderConfig? provider = null;
            try
            {
                provider = GetOllamaProvider(await settingsService.GetSettings());
                using OllamaApiClient client = new(provider.EndpointUrl);
                string? lastStatus = null;
                await foreach (PullModelResponse? response in client.PullModelAsync(new PullModelRequest { Model = modelId, Stream = true }, cts.Token))
                {
                    if (response is not null)
                    {
                        lastStatus = response.Status;
                        pullStates[modelId] = new AiOllamaModelState
                        {
                            ModelId = modelId,
                            DownloadStatus = AiOllamaModelState.kStatusDownloading,
                            DownloadProgress = (int)response.Percent
                        };
                    }
                }

                if (!string.Equals(lastStatus, "success", StringComparison.OrdinalIgnoreCase))
                {
                    pullStates[modelId] = new AiOllamaModelState { ModelId = modelId, DownloadStatus = AiOllamaModelState.kStatusFailed, DownloadProgress = 0 };
                }
                else
                {
                    pullStates.TryRemove(modelId, out _);
                }
            }
            catch (OperationCanceledException)
            {
                pullStates.TryRemove(modelId, out _);
            }
            catch (Exception exception)
            {
                pullStates[modelId] = new AiOllamaModelState { ModelId = modelId, DownloadStatus = AiOllamaModelState.kStatusFailed, DownloadProgress = 0 };
                Log.WriteWarning("AI Ollama model pull", BuildRequestErrorMessage(provider?.EndpointUrl ?? "", exception));
            }
            finally
            {
                if (pullCancellations.TryRemove(modelId, out CancellationTokenSource? removed))
                {
                    removed.Dispose();
                }
                else
                {
                    cts.Dispose();
                }
            }
        }

        /// <summary>
        /// Builds the response lists for downloaded models and download state.
        /// </summary>
        internal static AiOllamaModelsResponse BuildModelsResponse(IEnumerable<AiOllamaModelState> downloadedModels, IEnumerable<AiOllamaModelState> currentPullStates)
        {
            return new AiOllamaModelsResponse
            {
                Downloads = currentPullStates
                    .OrderBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DownloadedModels = downloadedModels
                    .OrderBy(model => model.ModelId, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        /// <summary>
        /// Returns configured Ollama model ids that should be kept warm.
        /// </summary>
        internal static IEnumerable<string> GetEnabledOllamaModelIds(AiProviderConfig ollamaProvider)
        {
            return ollamaProvider.Models
                .Where(model => model.Enabled && !string.IsNullOrWhiteSpace(model.ModelId))
                .Select(model => model.ModelId);
        }

        private static AiProviderConfig? FindOllamaProvider(AiSettings settings)
        {
            return settings.Providers.FirstOrDefault(provider => provider.Kind == AiProviderKind.Ollama);
        }

        private static AiProviderConfig GetOllamaProvider(AiSettings settings)
        {
            return FindOllamaProvider(settings)
                ?? throw new InvalidOperationException("Ollama provider is not configured.");
        }

        private static string BuildRequestErrorMessage(string endpoint, Exception exception)
        {
            string requestTarget = string.IsNullOrWhiteSpace(endpoint) ? "Ollama" : endpoint;
            Exception rootCause = exception.GetBaseException();
            string rootCauseMessage = rootCause.Message;
            string message = string.Equals(exception.Message, rootCauseMessage, StringComparison.Ordinal)
                ? exception.Message
                : $"{exception.Message} ({rootCauseMessage})";
            return $"{requestTarget}: {message}";
        }
    }
}
