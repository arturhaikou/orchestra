using Microsoft.Extensions.AI;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Stubs;

/// <summary>
/// Phase 1 stub. Throws <see cref="NotImplementedException"/> for every method.
/// Replace with a real workspace-scoped <see cref="IChatClient"/> resolver in Phase 2.
/// </summary>
public sealed class StubAIProviderResolver : IAIProviderResolver
{
    /// <inheritdoc/>
    public Task<IChatClient> ResolveAsync(Guid workspaceId, string modelId, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
