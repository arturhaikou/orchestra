using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using Orchestra.Application.Common.Interfaces;
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

    public async Task<List<Ticket>> GetTicketsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .Include(t => t.Integration)
            .Include(t => t.Comments)
            .Where(t => t.WorkspaceId == workspaceId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Ticket>> GetInternalTicketsByWorkspaceAsync(
        Guid workspaceId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Use left join with TicketPriority to enable sorting by priority value
        var query = from ticket in _context.Tickets
                    join priority in _context.Set<TicketPriority>()
                        on ticket.PriorityId equals priority.Id into priorityGroup
                    from priority in priorityGroup.DefaultIfEmpty()
                    where ticket.WorkspaceId == workspaceId
                       && ticket.IsInternal // Only pure internal tickets, not materialized external
                    orderby (priority != null ? priority.Value : 2) descending, // Default to medium (2) if null
                            ticket.UpdatedAt descending
                    select ticket;

        return await query
            .Skip(offset)
            .Take(limit)
            .Include(t => t.Integration)
            .Include(t => t.Comments)
            .ToListAsync(cancellationToken);
    }

    public async Task<Ticket?> GetTicketByIdAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .Include(t => t.Integration)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
    }

    public async Task<Ticket?> GetTicketByExternalIdAsync(Guid integrationId, string externalTicketId, CancellationToken cancellationToken = default)
    {
        return await _context.Tickets
            .Include(t => t.Integration)
            .Include(t => t.Comments)
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
}