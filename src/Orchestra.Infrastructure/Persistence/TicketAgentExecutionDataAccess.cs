using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence;

/// <summary>
/// Data access for querying tickets eligible for agent execution.
/// </summary>
public class TicketAgentExecutionDataAccess : ITicketAgentExecutionDataAccess
{
    private readonly AppDbContext _context;

    // Status GUIDs from seeding
    private static readonly Guid ToDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public TicketAgentExecutionDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Ticket>> GetInternalTicketsReadyForAgentAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Ticket>()
            .Where(t => t.IsInternal
                     && t.AssignedAgentId != null
                     && t.StatusId == ToDoStatusId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Ticket>> GetExternalMaterializedTicketsReadyForAgentAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Ticket>()
            .Where(t => !t.IsInternal
                     && t.AssignedAgentId != null
                     && t.IntegrationId != null
                     && (t.StatusId == ToDoStatusId || t.StatusId == null)) // Include null for legacy tickets
            .ToListAsync(cancellationToken);
    }
}
