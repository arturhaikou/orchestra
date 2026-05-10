using Orchestra.Application.Agents.DTOs;

namespace Orchestra.Application.Common.Interfaces;

public interface IAgentToolAssignmentService
{
    Task SaveAssignmentsAsync(
        Guid userId,
        Guid agentId,
        SaveAgentToolsRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentToolAssignmentsDto> GetAssignmentsAsync(
        Guid userId,
        Guid agentId,
        CancellationToken cancellationToken = default);
}
