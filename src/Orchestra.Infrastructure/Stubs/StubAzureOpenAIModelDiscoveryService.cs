using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Stubs;

/// <summary>
/// Phase 1 stub. Throws <see cref="NotImplementedException"/> for every method.
/// Replace with a real Azure OpenAI Management API client in Phase 2.
/// </summary>
public sealed class StubAzureOpenAIModelDiscoveryService : IAzureOpenAIModelDiscoveryService
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> DiscoverModelsAsync(string endpoint, string apiKey, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
