using Microsoft.Extensions.AI;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Resolves a workspace-scoped <see cref="IChatClient"/> for use in the agent execution pipeline.
/// Every workspace selects its own AI provider; this contract makes the workspace identifier
/// an explicit, compile-time-enforced parameter across the solution.
/// </summary>
public interface IChatClientResolver
{
    /// <summary>
    /// Returns an <see cref="IChatClient"/> configured for the AI provider of the specified workspace,
    /// with <paramref name="modelId"/> baked in at construction time.
    /// </summary>
    /// <param name="workspaceId">The workspace whose configured AI provider should be used.</param>
    /// <param name="modelId">
    /// The effective model identifier (never null). Resolved by the caller from workspace feature
    /// settings (<c>featureModel ?? workspace.DefaultModelId ?? throw</c>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="IChatClient"/> ready for API calls.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the workspace has no AI provider configured.
    /// </exception>
    Task<IChatClient> ResolveAsync(Guid workspaceId, string modelId, CancellationToken cancellationToken);
}
