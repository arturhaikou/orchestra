using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Application.Tickets.Common;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Service implementation for managing ticket comments.
/// Handles adding comments to both internal and external tickets.
/// Extracted logic from TicketService.AddCommentAsync and helpers.
/// </summary>
public class TicketCommentService : ITicketCommentService
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IUserDataAccess _userDataAccess;
    private readonly ITicketProviderFactory _ticketProviderFactory;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly ITicketIdParsingService _ticketIdParsingService;

    public TicketCommentService(
        ITicketDataAccess ticketDataAccess,
        IIntegrationDataAccess integrationDataAccess,
        IUserDataAccess userDataAccess,
        ITicketProviderFactory ticketProviderFactory,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        ITicketIdParsingService ticketIdParsingService)
    {
        _ticketDataAccess = ticketDataAccess;
        _integrationDataAccess = integrationDataAccess;
        _userDataAccess = userDataAccess;
        _ticketProviderFactory = ticketProviderFactory;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _ticketIdParsingService = ticketIdParsingService;
    }

    /// <summary>
    /// Adds a comment to a ticket (internal or external). Routes to the correct method based on ticketId format.
    /// </summary>
    public async Task<CommentDto> AddCommentAsync(
        string ticketId,
        Guid userId,
        AddCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Comment content cannot be empty.", nameof(request));

        var parseResult = _ticketIdParsingService.Parse(ticketId);
        if (parseResult.Type == TicketIdType.Internal)
        {
            return await AddCommentToInternalTicketAsync(ticketId, userId, request, cancellationToken);
        }
        else if (parseResult.Type == TicketIdType.External)
        {
            return await AddCommentToExternalTicketAsync(ticketId, userId, request, cancellationToken);
        }
        else
        {
            throw new ArgumentException($"Invalid ticket ID format: {ticketId}", nameof(ticketId));
        }
    }

    /// <summary>
    /// Adds a comment to an internal ticket.
    /// Extracted logic from TicketService.AddCommentToInternalTicketAsync (exact code, no changes).
    /// </summary>
    public async Task<CommentDto> AddCommentToInternalTicketAsync(
        string ticketId,
        Guid userId,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        // Parse ticket ID as GUID
        if (!Guid.TryParse(ticketId, out var ticketGuid))
        {
            throw new ArgumentException($"Invalid ticket ID format: {ticketId}", nameof(ticketId));
        }

        // Load ticket from database
        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketGuid, cancellationToken);
        if (ticket == null)
        {
            throw new TicketNotFoundException(ticketId);
        }

        // Validate user has access to ticket's workspace
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId,
            ticket.WorkspaceId,
            cancellationToken);

        // Fetch author name from database
        var user = await _userDataAccess.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        // Create comment using domain factory
        var comment = TicketComment.Create(
            ticketGuid,
            user.Name,
            request.Content);

        // Persist to database
        await _ticketDataAccess.AddCommentAsync(comment, cancellationToken);

        // Return DTO
        return new CommentDto(
            comment.Id.ToString(),
            comment.Author,
            comment.Content,
            comment.CreatedAt);
    }

    /// <summary>
    /// Adds a comment to an external ticket via provider API.
    /// Extracted logic from TicketService.AddCommentToExternalTicketAsync (exact code, no changes).
    /// </summary>
    public async Task<CommentDto> AddCommentToExternalTicketAsync(
        string ticketId,
        Guid userId,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        // Parse composite ID to extract integration and external ticket IDs
        var parts = ticketId.Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var integrationId))
        {
            throw new ArgumentException(
                $"Invalid composite ID format: '{ticketId}'. Expected format: '{{integrationId}}:{{externalTicketId}}'",
                nameof(ticketId));
        }

        var externalTicketId = parts[1];

        // Load integration
        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken);
        if (integration == null)
        {
            throw new TicketNotFoundException(ticketId);
        }

        // Validate workspace access
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId,
            integration.WorkspaceId,
            cancellationToken);

        // Get provider
        var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
        if (provider == null)
        {
            throw new InvalidOperationException(
                $"No provider implementation found for '{integration.Provider}'");
        }

        // Fetch author name from database
        var user = await _userDataAccess.FindByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID '{userId}' not found.");
        }

        // Call provider API
        try
        {
            var commentDto = await provider.AddCommentAsync(
                integration,
                externalTicketId,
                request.Content,
                user.Name,
                cancellationToken);

            return commentDto;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Failed to add comment to external ticket '{ticketId}' via provider '{integration.Provider}': {ex.Message}",
                ex);
        }
    }
}
