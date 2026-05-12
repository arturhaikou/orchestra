namespace Orchestra.Application.Agents.Services;

public interface IAgentSubAgentAssignmentService
{
    /// <summary>
    /// Validates and assigns sub-agents to a parent agent.
    /// Enforces workspace boundary, self-assignment prevention, and direct circular reference detection.
    /// </summary>
    /// <param name="parentAgentId">The parent agent identifier.</param>
    /// <param name="subAgentIds">The sub-agent IDs to assign.</param>
    /// <param name="workspaceId">The workspace the parent agent belongs to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AssignSubAgentsAsync(
        Guid parentAgentId,
        List<Guid> subAgentIds,
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
