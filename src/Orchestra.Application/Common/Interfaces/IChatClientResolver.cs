using Microsoft.Extensions.AI;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Factory for creating IChatClient instances based on provider and model identifier.
/// Abstracts the provider-specific logic (Azure OpenAI, Ollama, AWS, etc.) behind a single interface.
/// Enables workspace-level AI model selection by creating model-specific clients on-demand.
/// </summary>
public interface IChatClientResolver
{
    /// <summary>
    /// Resolves and returns an IChatClient instance for the specified model.
    /// </summary>
    /// <param name="modelId">
    /// Optional workspace-configured model identifier.
    /// If null, the startup-configured default IChatClient is returned.
    /// If non-null but unavailable (stale), silently falls back to the default.
    /// If non-null and available, creates and returns a new IChatClient configured for that model.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for async initialization of new clients.</param>
    /// <returns>
    /// An IChatClient instance ready to use for API calls.
    /// Never null.
    /// </returns>
    Task<IChatClient> ResolveChatClientAsync(string? modelId, CancellationToken cancellationToken = default);
}
