using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Exceptions;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Interface for agent services.
/// </summary>
public interface IAgentService
{
    /// <summary>
    /// Creates a new agent in the specified workspace.
    /// </summary>
    /// <param name="userId">The ID of the user creating the agent.</param>
    /// <param name="request">The create agent request containing agent details.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the created agent DTO.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    /// <exception cref="WorkspaceNotFoundException">Thrown when workspace is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user lacks access to workspace.</exception>
    Task<AgentDto> CreateAgentAsync(Guid userId, CreateAgentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all agents in the specified workspace.
    /// </summary>
    /// <param name="userId">The ID of the user requesting agents.</param>
    /// <param name="workspaceId">The workspace ID to retrieve agents for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of agent DTOs.</returns>
    /// <exception cref="WorkspaceNotFoundException">Thrown when workspace is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user lacks access to workspace.</exception>
    Task<List<AgentDto>> GetAgentsByWorkspaceIdAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single agent by ID.
    /// </summary>
    /// <param name="userId">The ID of the user requesting the agent.</param>
    /// <param name="agentId">The agent ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent DTO.</returns>
    /// <exception cref="AgentNotFoundException">Thrown when agent is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user lacks access to agent's workspace.</exception>
    Task<AgentDto> GetAgentByIdAsync(Guid userId, Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing agent.
    /// </summary>
    /// <param name="userId">The ID of the user updating the agent.</param>
    /// <param name="agentId">The agent ID to update.</param>
    /// <param name="request">The update request with nullable fields for partial updates.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated agent DTO.</returns>
    /// <exception cref="AgentNotFoundException">Thrown when agent is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user lacks access to agent's workspace.</exception>
    Task<AgentDto> UpdateAgentAsync(Guid userId, Guid agentId, UpdateAgentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an agent.
    /// </summary>
    /// <param name="userId">The ID of the user deleting the agent.</param>
    /// <param name="agentId">The agent ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="AgentNotFoundException">Thrown when agent is not found.</exception>
    /// <exception cref="UnauthorizedWorkspaceAccessException">Thrown when user lacks access to agent's workspace.</exception>
    Task DeleteAgentAsync(Guid userId, Guid agentId, CancellationToken cancellationToken = default);
}