using Orchestra.Application.Tickets.DTOs;
using Orchestra.Application.Common.Exceptions;

namespace Orchestra.Application.Common.Interfaces;

public interface ITicketService
{
    Task<TicketDto> CreateTicketAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of tickets for a workspace with hybrid retrieval.
    /// Fetches both internal tickets from DB and external tickets from providers,
    /// then merges them into a unified response.
    /// </summary>
    /// <param name="workspaceId">The workspace ID to retrieve tickets for</param>
    /// <param name="userId">The user ID for authorization</param>
    /// <param name="pageToken">Optional continuation token for pagination</param>
    /// <param name="pageSize">Number of items per page (default: 50, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated response containing merged internal and external tickets</returns>
    Task<PaginatedTicketsResponse> GetTicketsAsync(
        Guid workspaceId,
        Guid userId,
        string? pageToken = null,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single ticket by ID (supports both GUID and composite ID).
    /// </summary>
    /// <param name="ticketId">Internal ticket GUID or composite format "{integrationId}:{externalTicketId}"</param>
    /// <param name="userId">User ID from JWT claims for workspace authorization</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ticket details with comments</returns>
    /// <exception cref="TicketNotFoundException">Ticket not found in DB or provider</exception>
    /// <exception cref="UnauthorizedTicketAccessException">User lacks access to ticket's workspace</exception>
    Task<TicketDto> GetTicketByIdAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<TicketStatusDto>> GetAllStatusesAsync(CancellationToken cancellationToken = default);
    Task<List<TicketPriorityDto>> GetAllPrioritiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a ticket's assignments and metadata. 
    /// For external tickets, materializes DB record on first assignment.
    /// External tickets can only update assignments (AssignedAgentId, AssignedWorkflowId).
    /// Internal tickets can update status, priority, and assignments.
    /// </summary>
    /// <param name="ticketId">Internal ticket GUID or composite format "{integrationId}:{externalTicketId}"</param>
    /// <param name="userId">User ID from JWT claims for workspace authorization</param>
    /// <param name="request">Update request with nullable fields for partial updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated ticket details</returns>
    /// <exception cref="TicketNotFoundException">Ticket not found</exception>
    /// <exception cref="UnauthorizedTicketAccessException">User lacks access to ticket's workspace</exception>
    /// <exception cref="InvalidTicketOperationException">Attempted to update status/priority on external ticket</exception>
    Task<TicketDto> UpdateTicketAsync(
        string ticketId,
        Guid userId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an internal ticket to an external tracker ticket.
    /// Creates the ticket in the external system and updates the internal record with the reference.
    /// </summary>
    /// <param name="ticketId">The internal ticket ID (must be a GUID).</param>
    /// <param name="userId">The user ID for authorization checks.</param>
    /// <param name="integrationId">The tracker integration ID to create the ticket in.</param>
    /// <param name="issueTypeName">The issue type name (e.g., "Task", "Story", "Bug", "Epic").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated ticket DTO with external reference.</returns>
    /// <exception cref="TicketNotFoundException">Thrown when ticket is not found.</exception>
    /// <exception cref="IntegrationNotFoundException">Thrown when integration is not found.</exception>
    /// <exception cref="UnauthorizedTicketAccessException">Thrown when user lacks access to workspace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when ticket is already external or integration is invalid.</exception>
    Task<TicketDto> ConvertToExternalAsync(
        string ticketId,
        Guid userId,
        Guid integrationId,
        string issueTypeName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an internal ticket. External tickets cannot be deleted.
    /// </summary>
    /// <param name="ticketId">The ticket ID (must be internal GUID, not composite format)</param>
    /// <param name="userId">The ID of the user requesting the deletion</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="TicketNotFoundException">Thrown when ticket is not found</exception>
    /// <exception cref="UnauthorizedTicketAccessException">Thrown when user doesn't have access to ticket's workspace</exception>
    /// <exception cref="InvalidTicketOperationException">Thrown when attempting to delete an external ticket</exception>
    Task DeleteTicketAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a comment to a ticket (internal or external).
    /// For internal tickets, the comment is stored in the database.
    /// For external tickets, the comment is proxied to the provider API.
    /// </summary>
    /// <param name="ticketId">
    /// The ticket identifier. Can be either:
    /// - Internal ticket: GUID string (e.g., "3fa85f64-5717-4562-b3fc-2c963f66afa6")
    /// - External ticket: Composite format (e.g., "{integrationId}:{externalTicketId}")
    /// </param>
    /// <param name="userId">The user identifier for authorization checks.</param>
    /// <param name="request">The comment request containing content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created comment DTO.</returns>
    /// <exception cref="TicketNotFoundException">Thrown when the ticket does not exist.</exception>
    /// <exception cref="UnauthorizedTicketAccessException">Thrown when user lacks access to the ticket's workspace.</exception>
    /// <exception cref="ArgumentException">Thrown when content is empty or whitespace.</exception>
    Task<CommentDto> AddCommentAsync(
        string ticketId,
        Guid userId,
        AddCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an AI-powered summary for a ticket by combining its description and comments.
    /// The summary is generated on-demand and not persisted to the database.
    /// </summary>
    /// <param name="ticketId">
    /// The ticket identifier. Can be either:
    /// - Internal ticket: GUID string (e.g., "3fa85f64-5717-4562-b3fc-2c963f66afa6")
    /// - External ticket: Composite format (e.g., "{integrationId}:{externalTicketId}")
    /// </param>
    /// <param name="userId">The user identifier for authorization checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ticket DTO with the Summary field populated.</returns>
    /// <exception cref="TicketNotFoundException">Thrown when the ticket does not exist.</exception>
    /// <exception cref="UnauthorizedTicketAccessException">Thrown when user lacks access to the ticket's workspace.</exception>
    /// <exception cref="SummarizationException">Thrown when AI summarization fails.</exception>
    Task<TicketDto> GenerateSummaryAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default);
}