namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Assembles all runtime dependencies needed to serve an AG-UI streaming request
/// for a specific workspace agent, returning a typed record that Infrastructure
/// uses to construct the agent.
/// </summary>
public interface IAgentAGUIBuildService
{
    /// <summary>
    /// Validates access and retrieves the resolved AI provider, instructions, and tools
    /// for the given agent in the given workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace that owns the agent.</param>
    /// <param name="agentId">The unique identifier of the agent to build.</param>
    /// <param name="userId">The authenticated user; used for membership validation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="AgentAGUIContext"/> with everything needed to instantiate the agent,
    /// or <see langword="null"/> when the agent does not exist or does not belong to <paramref name="workspaceId"/>.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when <paramref name="userId"/> is not a member of <paramref name="workspaceId"/>.
    /// </exception>
    Task<AgentAGUIContext?> BuildAGUIAgentContextAsync(
        Guid workspaceId,
        Guid agentId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolved context that Infrastructure uses to construct an <c>AIAgent</c> for AG-UI streaming.
/// </summary>
/// <remarks>
/// For chat agents <see cref="IsCliAgent"/> is <see langword="false"/> and <see cref="ChatClient"/>
/// is populated. For CLI agents <see cref="IsCliAgent"/> is <see langword="true"/>,
/// <see cref="ChatClient"/> is <see langword="null"/>, and <see cref="AiCliIntegrationId"/>
/// holds the FK needed to open the subprocess client.
/// </remarks>
public sealed record AgentAGUIContext(
    string AgentName,
    string? Instructions,
    bool IsCliAgent,
    Microsoft.Extensions.AI.IChatClient? ChatClient,
    IEnumerable<Microsoft.Extensions.AI.AIFunction> Tools,
    Guid? AiCliIntegrationId = null);
