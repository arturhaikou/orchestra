namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Queries an Azure OpenAI resource and returns the names of its available model deployments.
/// This service is stateless — it accepts raw credential parameters rather than a workspace ID,
/// so implementations can be used without workspace-scoped lifetimes.
/// </summary>
public interface IAzureOpenAIModelDiscoveryService
{
    /// <summary>
    /// Connects to the Azure OpenAI resource at <paramref name="endpoint"/> using
    /// <paramref name="apiKey"/> and returns the deployment names found.
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI endpoint URL (plaintext, not encrypted).</param>
    /// <param name="apiKey">The Azure OpenAI API key (plaintext, not encrypted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of deployment names available at the given endpoint.</returns>
    Task<IReadOnlyList<string>> DiscoverModelsAsync(string endpoint, string apiKey, CancellationToken cancellationToken);
}
