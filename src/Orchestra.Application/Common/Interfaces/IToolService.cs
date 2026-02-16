using Orchestra.Application.Tools.DTOs;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for managing tool discovery, filtering, and agent assignments.
/// </summary>
public interface IToolService
{
    /// <summary>
    /// Gets available tools filtered by workspace integrations.
    /// Returns hierarchical structure with categories containing actions.
    /// </summary>
    /// <param name="userId">User ID for authorization.</param>
    /// <param name="workspaceId">Workspace ID to filter tools by connected integrations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hierarchical list of tool categories with nested actions.</returns>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user is not a workspace member.</exception>
    Task<List<ToolCategoryDto>> GetAvailableToolsAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets flat list of tool actions assigned to a specific agent.
    /// </summary>
    /// <param name="userId">User ID for authorization.</param>
    /// <param name="agentId">Agent ID to retrieve assigned tools for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Flat list of tool actions assigned to the agent.</returns>
    /// <exception cref="AgentNotFoundException">Thrown when agent is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user is not a workspace member.</exception>
    Task<List<ToolActionDto>> GetAgentToolActionsAsync(
        Guid userId,
        Guid agentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns tool actions to an agent.
    /// Duplicates are ignored (upsert behavior).
    /// </summary>
    /// <param name="userId">User ID for authorization.</param>
    /// <param name="agentId">Agent ID to assign tools to.</param>
    /// <param name="toolActionIds">List of tool action IDs to assign.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AgentNotFoundException">Thrown when agent is not found.</exception>
    /// <exception cref="ArgumentException">Thrown when any tool action ID does not exist.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user is not a workspace member.</exception>
    Task AssignToolActionsToAgentAsync(
        Guid userId,
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes tool actions from an agent.
    /// </summary>
    /// <param name="userId">User ID for authorization.</param>
    /// <param name="agentId">Agent ID to remove tools from.</param>
    /// <param name="toolActionIds">List of tool action IDs to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AgentNotFoundException">Thrown when agent is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user is not a workspace member.</exception>
    Task RemoveToolActionsFromAgentAsync(
        Guid userId,
        Guid agentId,
        List<Guid> toolActionIds,
        CancellationToken cancellationToken = default);
}