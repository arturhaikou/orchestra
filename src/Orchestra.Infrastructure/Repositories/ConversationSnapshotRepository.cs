using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Infrastructure.Repositories;

public class ConversationSnapshotRepository(AppDbContext db) : IConversationSnapshotRepository
{
    public Task<AgentConversationSnapshot?> GetByJobAndAgentAsync(
        Guid jobId,
        Guid agentId,
        CancellationToken cancellationToken = default)
        => db.AgentConversationSnapshots
            .FirstOrDefaultAsync(s => s.JobId == jobId && s.AgentId == agentId, cancellationToken);

    public async Task SaveAsync(
        AgentConversationSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetByJobAndAgentAsync(snapshot.JobId, snapshot.AgentId, cancellationToken);

        if (existing is null)
            db.AgentConversationSnapshots.Add(snapshot);
        else
            existing.Update(snapshot.SerializedSessionJson);

        await db.SaveChangesAsync(cancellationToken);
    }
}
