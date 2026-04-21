using Microsoft.Extensions.AI;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Resolves a fully configured <see cref="IChatClient"/> for a given workspace's AI provider.
/// This is the workspace-scoped replacement for the removed global <c>IChatClient</c> singleton.
/// </summary>
/// <remarks>
/// Implementations must throw when the workspace has no provider configured rather than returning
/// <see langword="null"/>, preventing silent execution with an undefined provider context.
/// </remarks>
public interface IAIProviderResolver
{
    /// <summary>
    /// Returns an <see cref="IChatClient"/> configured for the AI provider of the specified workspace,
    /// with <paramref name="modelId"/> baked in at construction time.
    /// </summary>
    /// <param name="workspaceId">The workspace whose configured AI provider should be resolved.</param>
    /// <param name="modelId">
    /// The effective model identifier (never null). For Azure OpenAI this is the deployment name
    /// (e.g. <c>"gpt-4o"</c>); for Ollama the model tag (e.g. <c>"llama3.1"</c>).
    /// The caller is responsible for resolving the fallback chain
    /// (<c>featureModel ?? workspace.DefaultModelId ?? throw</c>) before calling this method.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ready-to-use <see cref="IChatClient"/> for the workspace's provider.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the workspace identified by <paramref name="workspaceId"/> has no AI provider configured.
    /// </exception>
    Task<IChatClient> ResolveAsync(Guid workspaceId, string modelId, CancellationToken cancellationToken);
}
