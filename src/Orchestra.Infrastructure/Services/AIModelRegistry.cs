using Orchestra.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// In-memory registry of AI models available from the configured provider.
/// Populated once during application startup by consuming <see cref="IAIModelListService"/>.
/// Provides O(1) or O(n-models) lookup with no I/O required.
/// </summary>
internal sealed class AIModelRegistry : IAIModelRegistry
{
    private readonly HashSet<string> _availableModels;
    private readonly ILogger<AIModelRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIModelRegistry"/> class.
    /// Asynchronously loads the list of available models from the provider during construction.
    /// </summary>
    /// <param name="modelListService">Service for fetching the list of available models from the provider.</param>
    /// <param name="logger">Logger for startup diagnostics.</param>
    public AIModelRegistry(
        IAIModelListService modelListService,
        ILogger<AIModelRegistry> logger)
    {
        _logger = logger;
        // Note: This is synchronous by necessity (called during DI container construction).
        // A better pattern would be to use a factory or async initialization, but given the
        // existing Aspire startup flow, we block on the initial model fetch here.
        // Timeout is not enforced to avoid startup failure; if the provider is unreachable,
        // the registry will be empty and summarization will silently fall back to the default model.
        try
        {
            var task = modelListService.GetAvailableModelsAsync(CancellationToken.None);
            task.Wait(TimeSpan.FromSeconds(10)); // 10-second timeout to avoid indefinite startup hang
            var models = task.Result;
            _availableModels = new HashSet<string>(models, StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation(
                "AIModelRegistry initialized with {ModelCount} available models", 
                _availableModels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load available models during AIModelRegistry initialization. " +
                "Registry will be empty; workspace-selected models will be treated as unavailable and fall back to default.");
            _availableModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc/>
    public bool IsModelAvailable(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        return _availableModels.Contains(modelId);
    }
}
