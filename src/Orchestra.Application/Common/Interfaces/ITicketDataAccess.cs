using Orchestra.Domain.Entities;

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
    /// Retrieves all tickets for a workspace including both internal and materialized external tickets.
    /// Includes related entities: Integration, Comments.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of tickets ordered by creation date descending</returns>
    Task<List<Ticket>> GetTicketsByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves internal tickets for a workspace with pagination support.
    /// Returns pure internal tickets (IsInternal=true) and materialized external tickets (IntegrationId != null).
    /// Results are ordered by priority descending, then updated date descending.
    /// Includes related entities: Status, Priority, Comments.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to filter by</param>
    /// <param name="offset">Number of tickets to skip (0-based)</param>
    /// <param name="limit">Maximum number of tickets to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of internal tickets</returns>
    Task<List<Ticket>> GetInternalTicketsByWorkspaceAsync(
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
}