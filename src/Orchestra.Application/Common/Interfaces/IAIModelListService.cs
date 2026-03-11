namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for retrieving the list of AI models available from the currently-configured provider.
/// Abstracts over provider-specific implementations (Ollama, Azure OpenAI) to allow the API layer
/// to query models without knowing which provider is active.
/// </summary>
public interface IAIModelListService
{
    /// <summary>
    /// Fetches the list of available model names from the active AI provider.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A read-only list of model names. The exact meaning of each name depends on the provider:
    /// - For Ollama: model tags as they appear in the Ollama registry (e.g., "llama3.2", "mistral").
    /// - For Azure: deployment names for Azure OpenAI (e.g., "gpt-4o-mini", "gpt-4o").
    /// Returns an empty list if no models are available or configured.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the service is misconfigured (e.g., missing connection credentials or base URL).
    /// </exception>
    /// <exception cref="HttpRequestException">
    /// Thrown if the HTTP call to the provider fails (network error, provider is unreachable, etc.).
    /// </exception>
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default);
}
