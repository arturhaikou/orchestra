using Microsoft.Extensions.AI;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Pass-through adapter implementing <see cref="IChatClientResolver"/>.
/// Delegates entirely to <see cref="IAIProviderResolver"/>; contains no provider-specific logic.
/// </summary>
public sealed class ChatClientResolver : IChatClientResolver
{
    private readonly IAIProviderResolver _aiProviderResolver;

    public ChatClientResolver(IAIProviderResolver aiProviderResolver)
    {
        _aiProviderResolver = aiProviderResolver;
    }

    /// <inheritdoc/>
    public Task<IChatClient> ResolveAsync(Guid workspaceId, string modelId, CancellationToken cancellationToken)
        => _aiProviderResolver.ResolveAsync(workspaceId, modelId, cancellationToken);
}
