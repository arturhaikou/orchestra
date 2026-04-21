using Orchestra.Domain.Entities;
using Orchestra.Application.Tickets.DTOs;

namespace Orchestra.Application.Common.Interfaces;

public interface ITicketDataAccess
{
    Task<TicketStatus?> GetStatusByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<TicketPriority?> GetPriorityByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<TicketStatus?> GetStatusByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TicketPriority?> GetPriorityByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<TicketStatus>> GetAllStatusesAsync(CancellationToken cancellationToken = default);
    Task<List<TicketPriority>> GetAllPrioritiesAsync(CancellationToken cancellationToken = default);
    Task AddTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
    /// <summary>
    /// Updates an existing ticket in the database.
    /// </summary>
    /// <param name="ticket">The ticket to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves a page of tickets for a workspace (both internal and materialized external).
    /// Uses an explicit LEFT JOIN against Integrations — no .Include() calls.
    /// Results are ordered by CreatedAt descending. Skip/Take are applied server-side.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to filter by</param>
    /// <param name="offset">Zero-based number of rows to skip (≥ 0)</param>
    /// <param name="limit">Maximum number of rows to return (1–100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Flat projection DTOs for the requested page</returns>
    Task<List<TicketWithIntegrationDto>> GetTicketsByWorkspaceAsync(
        Guid workspaceId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns the total number of tickets (all types) for a workspace.
    /// Issued as a lightweight COUNT(*) query scoped by WorkspaceId.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to count tickets for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total ticket count (used to populate TotalCount in paginated responses)</returns>
    Task<int> CountTicketsByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves internal tickets for a workspace with pagination support.
    /// Returns only pure internal tickets (IsInternal=true).
    /// Results are ordered by priority value descending, then UpdatedAt descending.
    /// Uses a single explicit join query:
    ///   - LEFT OUTER JOIN on TicketPriorities (sort key + priority name/value/color)
    ///   - LEFT OUTER JOIN on Integrations (integration name)
    ///   - Correlated scalar sub-query COUNT(*) on TicketComments (comment count)
    /// No .Include() calls. No cartesian product expansion.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to filter by</param>
    /// <param name="offset">Number of tickets to skip (0-based)</param>
    /// <param name="limit">Maximum number of tickets to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// Flat projection DTOs for the requested page — at most <paramref name="limit"/> rows,
    /// one per ticket regardless of comment count.
    /// </returns>
    Task<List<InternalTicketListDto>> GetInternalTicketsByWorkspaceAsync(
        Guid workspaceId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets an internal ticket by its GUID with eager loading of relationships.
    /// </summary>
    /// <param name="ticketId">Internal ticket GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ticket entity or null if not found</returns>
    Task<Ticket?> GetTicketByIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets a materialized external ticket by integration ID and external ticket ID.
    /// </summary>
    /// <param name="integrationId">Integration GUID</param>
    /// <param name="externalTicketId">External ticket ID (e.g., "PROJ-123")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Materialized ticket entity or null if not materialized</returns>
    Task<Ticket?> GetTicketByExternalIdAsync(
        Guid integrationId,
        string externalTicketId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Deletes a ticket from the database by its ID.
    /// Associated comments are cascade-deleted via EF Core configuration.
    /// </summary>
    /// <param name="ticketId">The ticket ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Adds a comment to an internal ticket in the database.
    /// </summary>
    /// <param name="comment">The ticket comment to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddCommentAsync(
        TicketComment comment,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves all comments for a specific ticket ordered by creation date.
    /// </summary>
    /// <param name="ticketId">The ticket ID to retrieve comments for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of comments ordered by CreatedAt ascending</returns>
    Task<List<TicketComment>> GetCommentsByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves all comments for the provided ticket IDs in a single batch query,
    /// keyed by TicketId. Returns an empty dictionary when <paramref name="ticketIds"/> is empty.
    /// Results within each group are ordered by <c>CreatedAt</c> ascending.
    /// </summary>
    /// <param name="ticketIds">Collection of ticket GUIDs to fetch comments for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping each TicketId to its ordered list of comments.</returns>
    Task<Dictionary<Guid, List<TicketComment>>> GetCommentsByTicketIdsAsync(
        IEnumerable<Guid> ticketIds,
        CancellationToken cancellationToken = default);
}