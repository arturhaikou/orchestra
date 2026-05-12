using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Application.Agents.Services;

public class AgentSubAgentAssignmentService : IAgentSubAgentAssignmentService
{
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IAgentSubAgentDataAccess _agentSubAgentDataAccess;

    public AgentSubAgentAssignmentService(
        IAgentDataAccess agentDataAccess,
        IAgentSubAgentDataAccess agentSubAgentDataAccess)
    {
        _agentDataAccess = agentDataAccess ?? throw new ArgumentNullException(nameof(agentDataAccess));
        _agentSubAgentDataAccess = agentSubAgentDataAccess ?? throw new ArgumentNullException(nameof(agentSubAgentDataAccess));
    }

    public async Task AssignSubAgentsAsync(
        Guid parentAgentId,
        List<Guid> subAgentIds,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        foreach (var subAgentId in subAgentIds)
        {
            await ValidateSubAgentAsync(parentAgentId, subAgentId, workspaceId, cancellationToken);
        }

        await _agentSubAgentDataAccess.AssignSubAgentsAsync(parentAgentId, subAgentIds, cancellationToken);
    }

    private async Task ValidateSubAgentAsync(
        Guid parentAgentId,
        Guid subAgentId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        if (subAgentId == parentAgentId)
            throw new ArgumentException($"Agent {parentAgentId} cannot be assigned as its own sub-agent.");

        var subAgent = await _agentDataAccess.GetByIdAsync(subAgentId, cancellationToken)
            ?? throw new AgentNotFoundException(subAgentId);

        if (subAgent.WorkspaceId != workspaceId)
            throw new ArgumentException(
                $"Sub-agent {subAgentId} belongs to a different workspace and cannot be assigned to this agent.");

        await EnsureNoCyclicReferenceAsync(parentAgentId, subAgentId, cancellationToken);
    }

    private async Task EnsureNoCyclicReferenceAsync(
        Guid parentAgentId,
        Guid subAgentId,
        CancellationToken cancellationToken)
    {
        // Direct cycle: sub-agent already has the parent as one of its own sub-agents (A→B, B→A)
        var subAgentsOfSubAgent = await _agentSubAgentDataAccess
            .GetSubAgentIdsByParentAgentIdAsync(subAgentId, cancellationToken);

        if (subAgentsOfSubAgent.Contains(parentAgentId))
            throw new ArgumentException(
                $"Assigning agent {subAgentId} as a sub-agent of {parentAgentId} would create a circular reference.");
    }
}
