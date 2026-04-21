using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence;

public class TicketDataAccess : ITicketDataAccess
{
    private readonly AppDbContext _context;

    public TicketDataAccess(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TicketStatus?> GetStatusByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TicketStatus>()
            .FirstOrDefaultAsync(ts => ts.Name == name, cancellationToken);
    }

    public async Task<TicketPriority?> GetPriorityByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TicketPriority>()
            .FirstOrDefaultAsync(tp => tp.Name == name, cancellationToken);
    }

    public async Task<TicketStatus?> GetStatusByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TicketStatus>()
            .FirstOrDefaultAsync(ts => ts.Id == id, cancellationToken);
    }

    public async Task<TicketPriority?> GetPriorityByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<TicketPriority>()
            .FirstOrDefaultAsync(tp => tp.Id == id, cancellationToken);
    }

    public async Task<List<TicketStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<TicketStatus>()
            .OrderBy(ts => ts.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TicketPriority>> GetAllPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<TicketPriority>()
            .OrderBy(tp => tp.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        await _context.Set<Ticket>().AddAsync(ticket, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        _context.Set<Ticket>().Update(ticket);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TicketWithIntegrationDto>> GetTicketsByWorkspaceAsync(
        Guid workspaceId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = from t in _context.Tickets
                    join i in _context.Set<Integration>()
                        on t.IntegrationId equals i.Id into integrationGroup
                    from i in integrationGroup.DefaultIfEmpty() // LEFT JOIN — ticket.IntegrationId is nullable
                    where t.WorkspaceId == workspaceId
                    orderby t.CreatedAt descending
                    select new TicketWithIntegrationDto
                    {
                        Id = t.Id,
                        WorkspaceId = t.WorkspaceId,
                        Title = t.Title,
                        Description = t.Description,
                        PriorityId = t.PriorityId,
                        StatusId = t.StatusId,
                        IsInternal = t.IsInternal,
                        IntegrationId = t.IntegrationId,
                        ExternalTicketId = t.ExternalTicketId,
                        AssignedAgentId = t.AssignedAgentId,
                        AssignedWorkflowId = t.AssignedWorkflowId,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt,
                        IntegrationName = i != null ? i.Name : null,
                        IntegrationUrl = i != null ? i.Url : null,
                        IntegrationProvider = i != null ? i.Provider.ToString() : null
                    };

        return await query
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountTicketsByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .Where(t => t.WorkspaceId == workspaceId)
            .CountAsync(cancellationToken);
    }

    public async Task<List<InternalTicketListDto>> GetInternalTicketsByWorkspaceAsync(
        Guid workspaceId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Single explicit join query — no .Include() calls anywhere.
        // LEFT JOIN on TicketPriorities: sort key + priority name/value/color projected directly.
        // LEFT JOIN on Integrations: integration name projected directly (null when no integration).
        // Correlated scalar sub-query COUNT(*) on TicketComments: avoids cartesian expansion.
        var query = from ticket in _context.Tickets
                    join priority in _context.Set<TicketPriority>()
                        on ticket.PriorityId equals priority.Id into priorityGroup
                    from priority in priorityGroup.DefaultIfEmpty() // LEFT JOIN — PriorityId is nullable
                    join integration in _context.Set<Integration>()
                        on ticket.IntegrationId equals integration.Id into integrationGroup
                    from integration in integrationGroup.DefaultIfEmpty() // LEFT JOIN — IntegrationId is nullable
                    where ticket.WorkspaceId == workspaceId
                       && ticket.IsInternal
                    orderby (priority != null ? priority.Value : 0) descending,
                            ticket.UpdatedAt descending
                    select new InternalTicketListDto
                    {
                        Id = ticket.Id,
                        WorkspaceId = ticket.WorkspaceId,
                        Title = ticket.Title,
                        Description = ticket.Description,
                        StatusId = ticket.StatusId,
                        PriorityId = ticket.PriorityId,
                        PriorityValue = priority != null ? priority.Value : 0,
                        PriorityName = priority != null ? priority.Name : null,
                        PriorityColor = priority != null ? priority.Color : null,
                        IntegrationName = integration != null ? integration.Name : null,
                        IntegrationId = ticket.IntegrationId,
                        ExternalTicketId = ticket.ExternalTicketId,
                        AssignedAgentId = ticket.AssignedAgentId,
                        AssignedWorkflowId = ticket.AssignedWorkflowId,
                        IsInternal = ticket.IsInternal,
                        // Correlated scalar sub-query: COUNT(*) — resolves server-side, no collection loaded
                        CommentCount = _context.Set<TicketComment>().Count(c => c.TicketId == ticket.Id),
                        CreatedAt = ticket.CreatedAt,
                        UpdatedAt = ticket.UpdatedAt
                    };

        return await query
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<Ticket?> GetTicketByIdAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
    }

    public async Task<Ticket?> GetTicketByExternalIdAsync(Guid integrationId, string externalTicketId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .FirstOrDefaultAsync(
                t => t.IntegrationId == integrationId && t.ExternalTicketId == externalTicketId,
                cancellationToken);
    }

    public async Task DeleteTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _context.Tickets.FindAsync(new object[] { ticketId }, cancellationToken);
        if (ticket != null)
        {
            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddCommentAsync(
        TicketComment comment,
        CancellationToken cancellationToken = default)
    {
        await _context.Set<TicketComment>().AddAsync(comment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TicketComment>> GetCommentsByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<TicketComment>()
            .Where(tc => tc.TicketId == ticketId)
            .OrderBy(tc => tc.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, List<TicketComment>>> GetCommentsByTicketIdsAsync(
        IEnumerable<Guid> ticketIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ticketIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, List<TicketComment>>();

        var comments = await _context.Set<TicketComment>()
            .Where(c => ids.Contains(c.TicketId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return comments
            .GroupBy(c => c.TicketId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}