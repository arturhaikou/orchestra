using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

public interface IConversationSnapshotRepository
{
    Task<AgentConversationSnapshot?> GetByJobAndAgentAsync(
        Guid jobId,
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        AgentConversationSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
