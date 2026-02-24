using System.Collections.Generic;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Enumeration for pagination phases to avoid magic strings.
/// Follows SOLID principle by defining clear constants.
/// </summary>
internal static class TicketPaginationPhase
{
    public const string Internal = "internal";
    public const string External = "external";
}

public class TicketQueryService : ITicketQueryService
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ITicketProviderFactory _ticketProviderFactory;
    private readonly ITicketIdParsingService _ticketIdParsingService;
    private readonly ITicketMappingService _ticketMappingService;
    private readonly ITicketEnrichmentService _ticketEnrichmentService;
    private readonly IExternalTicketFetchingService _externalFetchingService;
    private readonly ISentimentAnalysisService _sentimentAnalysisService;
    private readonly ITicketPaginationService _ticketPaginationService;
    private readonly ILogger<TicketQueryService> _logger;

    public TicketQueryService(
        ITicketDataAccess ticketDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IIntegrationDataAccess integrationDataAccess,
        ITicketProviderFactory ticketProviderFactory,
        ITicketIdParsingService ticketIdParsingService,
        ITicketMappingService ticketMappingService,
        ITicketEnrichmentService ticketEnrichmentService,
        IExternalTicketFetchingService externalFetchingService,
        ISentimentAnalysisService sentimentAnalysisService,
        ITicketPaginationService ticketPaginationService,
        ILogger<TicketQueryService> logger)
    {
        _ticketDataAccess = ticketDataAccess;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _integrationDataAccess = integrationDataAccess;
        _ticketProviderFactory = ticketProviderFactory;
        _ticketIdParsingService = ticketIdParsingService;
        _ticketMappingService = ticketMappingService;
        _ticketEnrichmentService = ticketEnrichmentService;
        _externalFetchingService = externalFetchingService;
        _sentimentAnalysisService = sentimentAnalysisService;
        _ticketPaginationService = ticketPaginationService;
        _logger = logger;
    }

    public async Task<PaginatedTicketsResponse> GetTicketsAsync(
        Guid workspaceId,
        Guid userId,
        string? pageToken = null,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate input parameters and authorization
        ValidateInputParameters(workspaceId, userId);
        pageSize = _ticketPaginationService.NormalizePageSize(pageSize);
        await ValidateWorkspaceAccessAsync(workspaceId, userId, cancellationToken);

        // 2. Parse pagination token
        var currentToken = _ticketPaginationService.ParsePageToken(pageToken);

        // 3. Fetch tickets based on current phase
        var (resultTickets, isLastPage, nextToken) = currentToken.Phase == TicketPaginationPhase.Internal
            ? await FetchInternalPhaseAsync(workspaceId, currentToken, pageSize, cancellationToken)
            : await FetchExternalPhaseAsync(workspaceId, currentToken, pageSize, cancellationToken);

        // 4. Deduplicate and finalize response
        resultTickets = DeduplicateTickets(resultTickets);
        await CalculateSentimentForTicketsAsync(resultTickets, cancellationToken);

        var nextPageTokenString = GenerateNextPageToken(nextToken, isLastPage);

        _logger.LogInformation(
            "GetTicketsAsync complete: Returned {Count} tickets, IsLast: {IsLast}",
            resultTickets.Count, isLastPage);

        return new PaginatedTicketsResponse(
            Items: resultTickets,
            NextPageToken: nextPageTokenString,
            IsLast: isLastPage,
            TotalCount: resultTickets.Count
        );
    }

    public async Task<TicketDto> GetTicketByIdAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Fetching ticket {TicketId} for user {UserId}",
            ticketId, userId);

        if (string.IsNullOrWhiteSpace(ticketId))
        {
            _logger.LogWarning("GetTicketByIdAsync called with empty ticket ID");
            throw new ArgumentException("Ticket ID is required.", nameof(ticketId));
        }

        try
        {
            // Parse and validate ticket ID format
            var parseResult = _ticketIdParsingService.Parse(ticketId);
            
            if (parseResult.Type == TicketIdType.External)
            {
                // External ticket path (composite ID)
                return await GetExternalTicketByCompositeIdAsync(ticketId, userId, cancellationToken);
            }
            else
            {
                // Internal ticket path (GUID)
                return await GetInternalTicketByIdAsync(parseResult.InternalId!.Value, userId, cancellationToken);
            }
        }
        catch (TicketNotFoundException ex)
        {
            _logger.LogWarning(ex,
                "Ticket {TicketId} not found for user {UserId}",
                ticketId, userId);
            throw;
        }
        catch (UnauthorizedTicketAccessException ex)
        {
            _logger.LogWarning(ex,
                "User {UserId} unauthorized to access ticket {TicketId}",
                userId, ticketId);
            throw;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error fetching ticket {TicketId} for user {UserId}",
                ticketId, userId);
            throw;
        }
    }

    public async Task<List<TicketStatusDto>> GetAllStatusesAsync(CancellationToken cancellationToken = default)
    {
        var statuses = await _ticketDataAccess.GetAllStatusesAsync(cancellationToken);
        return statuses.Select(s => new TicketStatusDto(s.Id, s.Name, s.Color)).ToList();
    }

    public async Task<List<TicketPriorityDto>> GetAllPrioritiesAsync(CancellationToken cancellationToken = default)
    {
        var priorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);
        return priorities.Select(p => new TicketPriorityDto(p.Id, p.Name, p.Color, p.Value)).ToList();
    }

    #region GetTicketsAsync Helper Methods

    /// <summary>
    /// Validates input parameters for GetTicketsAsync.
    /// Follows Single Responsibility Principle by isolating parameter validation.
    /// </summary>
    private void ValidateInputParameters(Guid workspaceId, Guid userId)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
    }

    /// <summary>
    /// Validates that the user has access to the workspace.
    /// Handles authorization concerns separately from pagination logic.
    /// </summary>
    private async Task ValidateWorkspaceAccessAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId,
            workspaceId,
            cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedTicketAccessException(
                userId,
                $"workspace:{workspaceId}");
        }
    }

    /// <summary>
    /// Fetches internal tickets for Phase 1 pagination.
    /// Returns tickets, pagination state, and whether this is the last page.
    /// Separates internal ticket handling from external ticket handling.
    /// </summary>
    private async Task<(List<TicketDto> Tickets, bool IsLastPage, TicketPageToken? NextToken)> FetchInternalPhaseAsync(
        Guid workspaceId,
        TicketPageToken currentToken,
        int pageSize,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Fetching internal phase (offset: {Offset}, limit: {Limit})",
            currentToken.InternalOffset, pageSize);

        // Fetch internal tickets
        var internalTickets = await _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(
            workspaceId,
            currentToken.InternalOffset,
            pageSize,
            cancellationToken);

        var pureInternalTickets = internalTickets
            .Where(t => t.IntegrationId == null && string.IsNullOrEmpty(t.ExternalTicketId))
            .ToList();

        // Load lookups
        var allStatuses = await _ticketDataAccess.GetAllStatusesAsync(cancellationToken);
        var allPriorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);
        var statusLookup = allStatuses.ToDictionary(s => s.Id, s => s);
        var priorityLookup = allPriorities.ToDictionary(p => p.Id, p => p);

        // Map to DTOs
        var resultTickets = pureInternalTickets
            .Select(t => MapInternalTicketToDto(t, statusLookup, priorityLookup))
            .ToList();

        _logger.LogInformation(
            "Internal phase: Fetched {Count} tickets",
            resultTickets.Count);

        // If page is full, continue with internal phase
        if (resultTickets.Count >= pageSize)
        {
            var nextToken = new TicketPageToken
            {
                Phase = TicketPaginationPhase.Internal,
                InternalOffset = currentToken.InternalOffset + pageSize
            };
            return (resultTickets, false, nextToken);
        }

        // Page not full, try to fill with external tickets
        var remainingSlots = pageSize - resultTickets.Count;
        _logger.LogDebug(
            "Internal phase incomplete ({Count}/{PageSize}). Attempting to fill {Remaining} remaining slots with external tickets",
            resultTickets.Count, pageSize, remainingSlots);

        var (externalTickets, hasMore, externalState) = await FetchExternalTicketsToFillSlotsAsync(
            workspaceId,
            remainingSlots,
            null,
            statusLookup,
            priorityLookup,
            cancellationToken);

        resultTickets.AddRange(externalTickets);

        if (hasMore)
        {
            var nextToken = new TicketPageToken
            {
                Phase = TicketPaginationPhase.External,
                // Advance by the number of pure internal DB rows consumed, not by the total
                // result count (which also includes external tickets already fetched this page).
                InternalOffset = currentToken.InternalOffset + pureInternalTickets.Count,
                ExternalState = externalState
            };
            return (resultTickets, false, nextToken);
        }

        return (resultTickets, true, null);
    }

    /// <summary>
    /// Fetches external tickets for Phase 2 pagination.
    /// Returns tickets, pagination state, and whether this is the last page.
    /// </summary>
    private async Task<(List<TicketDto> Tickets, bool IsLastPage, TicketPageToken? NextToken)> FetchExternalPhaseAsync(
        Guid workspaceId,
        TicketPageToken currentToken,
        int pageSize,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Fetching external phase (limit: {Limit})",
            pageSize);

        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(
            workspaceId,
            cancellationToken);

        var trackerIntegrations = integrations
            .Where(i => i.Type == IntegrationType.TRACKER)
            .ToList();

        if (!trackerIntegrations.Any())
        {
            _logger.LogDebug("No tracker integrations found. External phase complete.");
            return (new List<TicketDto>(), true, null);
        }

        var externalTickets = await _externalFetchingService.FetchExternalTicketsAsync(
            trackerIntegrations,
            pageSize,
            currentToken.ExternalState,
            cancellationToken);

        _logger.LogInformation(
            "External phase: Fetched {Count} tickets, HasMore: {HasMore}",
            externalTickets.Tickets.Count, externalTickets.HasMore);

        if (externalTickets.HasMore)
        {
            var nextToken = new TicketPageToken
            {
                Phase = TicketPaginationPhase.External,
                InternalOffset = currentToken.InternalOffset,
                ExternalState = externalTickets.State
            };
            return (externalTickets.Tickets, false, nextToken);
        }

        return (externalTickets.Tickets, true, null);
    }

    /// <summary>
    /// Helper to fetch external tickets and fill remaining slots.
    /// Returns the updated <see cref="ExternalPaginationState"/> so the caller can embed it
    /// in the next-page token — without this the client would restart external pagination
    /// from scratch on every subsequent request.
    /// </summary>
    private async Task<(List<TicketDto> Tickets, bool HasMore, ExternalPaginationState? State)> FetchExternalTicketsToFillSlotsAsync(
        Guid workspaceId,
        int remainingSlots,
        ExternalPaginationState? externalState,
        Dictionary<Guid, TicketStatus> statusLookup,
        Dictionary<Guid, TicketPriority> priorityLookup,
        CancellationToken cancellationToken)
    {
        if (remainingSlots <= 0)
            return (new List<TicketDto>(), false, null);

        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(
            workspaceId,
            cancellationToken);

        var trackerIntegrations = integrations
            .Where(i => i.Type == IntegrationType.TRACKER)
            .ToList();

        if (!trackerIntegrations.Any())
            return (new List<TicketDto>(), false, null);

        var externalTickets = await _externalFetchingService.FetchExternalTicketsAsync(
            trackerIntegrations,
            remainingSlots,
            externalState,
            cancellationToken);

        var resultTickets = new List<TicketDto>();

        // Note: externalTickets returned from FetchExternalTicketsAsync should already have
        // assignedAgentId/assignedWorkflowId merged from materialized tickets if they exist.
        // We just need to add them directly to results.
        foreach (var extTicket in externalTickets.Tickets)
        {
            resultTickets.Add(extTicket);
        }

        // Propagate the updated state so the caller can persist it in the next-page token.
        return (resultTickets, externalTickets.HasMore, externalTickets.State);
    }

    /// <summary>
    /// Removes duplicate tickets by ID.
    /// Defensive deduplication as a separate concern.
    /// </summary>
    private List<TicketDto> DeduplicateTickets(List<TicketDto> tickets)
    {
        return tickets
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Generates the next page token string if needed.
    /// Separates token generation from business logic.
    /// </summary>
    private string? GenerateNextPageToken(TicketPageToken? nextToken, bool isLastPage)
    {
        if (nextToken == null || isLastPage)
            return null;

        return _ticketPaginationService.SerializePageToken(nextToken);
    }

    #endregion

    private async Task<TicketDto> GetInternalTicketByIdAsync(
        Guid ticketId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Fetching internal ticket {TicketId} from database",
            ticketId);

        // 1. Fetch ticket from database with eager loading
        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
        
        if (ticket == null)
        {
            _logger.LogWarning(
                "Internal ticket {TicketId} not found in database",
                ticketId);
            throw new TicketNotFoundException(ticketId.ToString());
        }

        // 1.5. Detect materialized external ticket and redirect to composite ID path
        if (ticket.IntegrationId.HasValue && !string.IsNullOrEmpty(ticket.ExternalTicketId))
        {
            _logger.LogDebug(
                "Ticket {TicketId} is a materialized external ticket. Redirecting to composite ID path.",
                ticketId);
            
            var compositeId = $"{ticket.IntegrationId.Value}:{ticket.ExternalTicketId}";
            return await GetExternalTicketByCompositeIdAsync(compositeId, userId, cancellationToken);
        }

        // 2. Validate workspace authorization
        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId,
            ticket.WorkspaceId,
            cancellationToken);
        
        if (!hasAccess)
        {
            _logger.LogWarning(
                "User {UserId} lacks access to workspace {WorkspaceId} for ticket {TicketId}",
                userId, ticket.WorkspaceId, ticketId);
            throw new UnauthorizedTicketAccessException(userId, ticketId.ToString());
        }

        // 3. Load status and priority lookup data
        TicketStatus? status = null;
        TicketPriority? priority = null;
        
        if (ticket.StatusId.HasValue)
        {
            var allStatuses = await _ticketDataAccess.GetAllStatusesAsync(cancellationToken);
            status = allStatuses.FirstOrDefault(s => s.Id == ticket.StatusId.Value);
        }
        
        if (ticket.PriorityId.HasValue)
        {
            var allPriorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);
            priority = allPriorities.FirstOrDefault(p => p.Id == ticket.PriorityId.Value);
        }

        // 4. Map comments with timestamp for internal tickets
        var commentDtos = ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.Id.ToString(),
                c.Author,
                c.Content,
                c.CreatedAt))
            .ToList();

        _logger.LogInformation(
            "Successfully fetched internal ticket {TicketId} with {CommentCount} comments",
            ticketId, commentDtos.Count);

        // 5. Map to TicketDto (pure internal ticket)
        var ticketDto = new TicketDto(
            Id: ticket.Id.ToString(),
            WorkspaceId: ticket.WorkspaceId,
            Title: ticket.Title,
            Description: ticket.Description ?? string.Empty,
            Status: status != null ? new TicketStatusDto(status.Id, status.Name, status.Color) : null,
            Priority: priority != null ? new TicketPriorityDto(priority.Id, priority.Name, priority.Color, priority.Value) : null,
            Internal: ticket.IsInternal,
            IntegrationId: ticket.IntegrationId,
            ExternalTicketId: ticket.ExternalTicketId,
            ExternalUrl: null,
            Source: "INTERNAL",
            AssignedAgentId: ticket.AssignedAgentId,
            AssignedWorkflowId: ticket.AssignedWorkflowId,
            Comments: commentDtos,
            Satisfaction: null,
            Summary: null
        );

        // 6. Calculate sentiment/satisfaction
        ticketDto = ticketDto with { Satisfaction = 100 };

        return ticketDto;
    }

    private async Task<TicketDto> GetExternalTicketByCompositeIdAsync(
        string compositeId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Parsing composite ticket ID: {CompositeId}",
            compositeId);

        // 1. Parse composite ID format using validator
        var parseResult = _ticketIdParsingService.Parse(compositeId);
        
        if (parseResult.Type != TicketIdType.External)
        {
            throw new ArgumentException(
                $"Expected external ticket ID in composite format, but received: '{compositeId}'",
                nameof(compositeId));
        }
        
        var integrationId = parseResult.IntegrationId!.Value;
        var externalTicketId = parseResult.ExternalTicketId!;

        _logger.LogDebug(
            "Parsed composite ID - Integration: {IntegrationId}, ExternalTicketId: {ExternalTicketId}",
            integrationId, externalTicketId);

        // 2. Load integration entity
        var integration = await _integrationDataAccess.GetByIdAsync(
            integrationId,
            cancellationToken);
        
        if (integration == null)
        {
            _logger.LogWarning(
                "Integration {IntegrationId} not found for composite ticket {CompositeId}",
                integrationId, compositeId);
            throw new TicketNotFoundException(compositeId);
        }

        // 3. Validate workspace authorization
        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId,
            integration.WorkspaceId,
            cancellationToken);
        
        if (!hasAccess)
        {
            _logger.LogWarning(
                "User {UserId} lacks access to workspace {WorkspaceId} for integration {IntegrationId}",
                userId, integration.WorkspaceId, integrationId);
            throw new UnauthorizedTicketAccessException(userId, compositeId);
        }

        // 4. Fetch ticket from provider and merge with DB assignments
        return await FetchAndMergeExternalTicketAsync(
            integration,
            externalTicketId,
            compositeId,
            cancellationToken);
    }

    private static string NormalizeExternalTicketId(string externalTicketId, string provider)
    {
        if (string.IsNullOrEmpty(externalTicketId)) return externalTicketId;
        // Remove leading '#' for GitHub and GitLab
        if ((provider.ToUpperInvariant().Contains("GITHUB") || provider.ToUpperInvariant().Contains("GITLAB")) && externalTicketId.StartsWith("#"))
            return externalTicketId.Substring(1);
        return externalTicketId;
    }

    private async Task<TicketDto> FetchAndMergeExternalTicketAsync(
        Integration integration,
        string externalTicketId,
        string compositeId,
        CancellationToken cancellationToken)
    {
        // Normalize external ticket id for provider
        var normalizedExternalTicketId = NormalizeExternalTicketId(externalTicketId, integration.Provider.ToString());
        // 1. Validate and decrypt integration credentials
        if (string.IsNullOrEmpty(integration.EncryptedApiKey))
        {
            throw new InvalidOperationException(
                $"Integration {integration.Id} is missing encrypted API key");
        }

        // 2. Get provider from factory
        var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
        if (provider == null)
        {
            throw new InvalidOperationException(
                $"No provider found for integration type '{integration.Provider}'");
        }

        // 3. Fetch ticket from provider
        _logger.LogDebug(
            "Fetching ticket {ExternalTicketId} from provider {Provider}",
            normalizedExternalTicketId, integration.Provider);

        ExternalTicketDto? externalTicket;
        try
        {
            externalTicket = await provider.GetTicketByIdAsync(
                integration,
                normalizedExternalTicketId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to fetch ticket {ExternalTicketId} from provider {Provider}",
                normalizedExternalTicketId, integration.Provider);
            throw new TicketNotFoundException(compositeId);
        }

        if (externalTicket == null)
        {
            throw new TicketNotFoundException(compositeId);
        }

        // 4. Check for materialized DB record
        var materializedTicket = await _ticketDataAccess.GetTicketByExternalIdAsync(
            integration.Id,
            normalizedExternalTicketId,
            cancellationToken);

        _logger.LogDebug(
            "Materialization check for ticket {ExternalTicketId}: {IsMaterialized}",
            externalTicketId, materializedTicket != null);

        // 5. Merge assignments if materialized
        Guid? assignedAgentId = null;
        Guid? assignedWorkflowId = null;
        TicketStatusDto? status = null;
        TicketPriorityDto? priority = null;
        List<CommentDto> mergedComments = externalTicket.Comments;
        
        if (materializedTicket != null)
        {
            assignedAgentId = materializedTicket.AssignedAgentId;
            assignedWorkflowId = materializedTicket.AssignedWorkflowId;
            
            // Use internal status/priority for materialized tickets if set
            if (materializedTicket.StatusId.HasValue)
            {
                var internalStatus = await _ticketDataAccess.GetStatusByIdAsync(
                    materializedTicket.StatusId.Value, 
                    cancellationToken);
                if (internalStatus != null)
                {
                    status = new TicketStatusDto(internalStatus.Id, internalStatus.Name, internalStatus.Color);
                }
            }
            
            if (materializedTicket.PriorityId.HasValue)
            {
                var internalPriority = await _ticketDataAccess.GetPriorityByIdAsync(
                    materializedTicket.PriorityId.Value, 
                    cancellationToken);
                if (internalPriority != null)
                {
                    priority = new TicketPriorityDto(
                        internalPriority.Id, 
                        internalPriority.Name, 
                        internalPriority.Color, 
                        internalPriority.Value);
                }
            }

            // Merge comments: combine external and internal comments, sort by timestamp descending
            var internalCommentDtos = materializedTicket.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentDto(
                    c.Id.ToString(),
                    c.Author,
                    c.Content,
                    c.CreatedAt))
                .ToList();

            mergedComments = externalTicket.Comments.Concat(internalCommentDtos)
                .OrderByDescending(c => c.Timestamp ?? DateTime.MinValue)
                .ToList();
        }
        
        // Fallback to external status/priority if not materialized or not set
        if (status == null && !string.IsNullOrEmpty(externalTicket.StatusName))
        {
            status = new TicketStatusDto(Guid.Empty, externalTicket.StatusName, externalTicket.StatusColor ?? "bg-gray-500");
        }
        
        if (priority == null && !string.IsNullOrEmpty(externalTicket.PriorityName))
        {
            priority = new TicketPriorityDto(Guid.Empty, externalTicket.PriorityName, externalTicket.PriorityColor ?? "bg-gray-500", externalTicket.PriorityValue);
        }

        _logger.LogInformation(
            "Successfully fetched and merged external ticket {CompositeId}",
            compositeId);

        // 6. Map to TicketDto (external format)
        var ticketDto = new TicketDto(
            Id: compositeId, // Composite ID format
            WorkspaceId: integration.WorkspaceId,
            Title: externalTicket.Title,
            Description: externalTicket.Description,
            Status: status,
            Priority: priority,
            Internal: false,
            IntegrationId: integration.Id,
            ExternalTicketId: externalTicketId,
            ExternalUrl: _ticketMappingService.BuildExternalUrl(integration, externalTicketId),
            Source: integration.Provider.ToString().ToUpperInvariant(),
            AssignedAgentId: assignedAgentId,
            AssignedWorkflowId: assignedWorkflowId,
            Comments: mergedComments,
            Satisfaction: null,
            Summary: null
        );

        // 7. Calculate sentiment/satisfaction
        ticketDto = await CalculateSentimentForSingleTicketAsync(ticketDto, cancellationToken);

        return ticketDto;
    }

    private TicketDto MapInternalTicketToDto(
        Ticket ticket, 
        Dictionary<Guid, TicketStatus> statusLookup,
        Dictionary<Guid, TicketPriority> priorityLookup)
    {
        var status = ticket.StatusId.HasValue && statusLookup.ContainsKey(ticket.StatusId.Value)
            ? statusLookup[ticket.StatusId.Value]
            : null;
        
        var priority = ticket.PriorityId.HasValue && priorityLookup.ContainsKey(ticket.PriorityId.Value)
            ? priorityLookup[ticket.PriorityId.Value]
            : null;
        
        // For materialized external tickets, use composite ID format instead of GUID
        var ticketId = (ticket.IntegrationId.HasValue && !string.IsNullOrEmpty(ticket.ExternalTicketId))
            ? $"{ticket.IntegrationId.Value}:{ticket.ExternalTicketId}"
            : ticket.Id.ToString();
        
        // Map comments with timestamp for internal tickets
        var commentDtos = ticket.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto(
                c.Id.ToString(),
                c.Author,
                c.Content,
                c.CreatedAt))
            .ToList();

        return new TicketDto(
            Id: ticketId,
            WorkspaceId: ticket.WorkspaceId,
            Title: ticket.Title,
            Description: ticket.Description,
            Status: status != null ? new TicketStatusDto(status.Id, status.Name, status.Color) : null,
            Priority: priority != null ? new TicketPriorityDto(priority.Id, priority.Name, priority.Color, priority.Value) : null,
            Internal: ticket.IsInternal,
            IntegrationId: ticket.IntegrationId,
            ExternalTicketId: ticket.ExternalTicketId,
            ExternalUrl: null,
            Source: "INTERNAL",
            AssignedAgentId: ticket.AssignedAgentId,
            AssignedWorkflowId: ticket.AssignedWorkflowId,
            Comments: commentDtos,
            Satisfaction: null,
            Summary: null
        );
    }

    private async Task<TicketDto> CalculateSentimentForSingleTicketAsync(
        TicketDto ticket,
        CancellationToken cancellationToken)
    {
        // Pure internal tickets always get 100
        if (ticket.Internal && ticket.IntegrationId == null)
        {
            return ticket with { Satisfaction = 100 };
        }

        // Tickets without comments get 100
        if (ticket.Comments == null || ticket.Comments.Count == 0)
        {
            return ticket with { Satisfaction = 100 };
        }

        // External tickets with comments need sentiment analysis
        var commentContents = ticket.Comments
            .Select(c => c.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (commentContents.Count == 0)
        {
            return ticket with { Satisfaction = 100 };
        }

        try
        {
            var sentimentRequest = new TicketSentimentRequest(
                ticket.WorkspaceId,
                ticket.Id,
                commentContents
            );

            var sentimentResults = await _sentimentAnalysisService.AnalyzeBatchSentimentAsync(
                new List<TicketSentimentRequest> { sentimentRequest },
                cancellationToken);

            var result = sentimentResults.FirstOrDefault();
            if (result != null)
            {
                _logger.LogInformation(
                    "Sentiment analysis complete for ticket {TicketId}: {Sentiment}",
                    ticket.Id, result.Sentiment);
                
                return ticket with { Satisfaction = result.Sentiment };
            }

            return ticket with { Satisfaction = 100 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sentiment for ticket {TicketId}, defaulting to 100", ticket.Id);
            return ticket with { Satisfaction = 100 };
        }
    }

    private async Task CalculateSentimentForTicketsAsync(
        List<TicketDto> tickets,
        CancellationToken cancellationToken)
    {
        if (tickets == null || tickets.Count == 0)
            return;

        var ticketsToAnalyze = new List<TicketSentimentRequest>();
        var ticketIndexMap = new Dictionary<string, int>();

        for (int i = 0; i < tickets.Count; i++)
        {
            var ticket = tickets[i];

            if (ticket.Internal && ticket.IntegrationId == null)
            {
                tickets[i] = ticket with { Satisfaction = 100 };
                continue;
            }

            if (ticket.Comments == null || ticket.Comments.Count == 0)
            {
                tickets[i] = ticket with { Satisfaction = 100 };
                continue;
            }

            var commentContents = ticket.Comments
                .Select(c => c.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (commentContents.Count > 0)
            {
                ticketsToAnalyze.Add(new TicketSentimentRequest(
                    ticket.WorkspaceId,
                    ticket.Id,
                    commentContents
                ));
                ticketIndexMap[ticket.Id] = i;
            }
            else
            {
                tickets[i] = ticket with { Satisfaction = 100 };
            }
        }

        if (ticketsToAnalyze.Count > 0)
        {
            try
            {
                var sentimentResults = await _sentimentAnalysisService.AnalyzeBatchSentimentAsync(
                    ticketsToAnalyze,
                    cancellationToken);

                foreach (var result in sentimentResults)
                {
                    if (ticketIndexMap.TryGetValue(result.TicketId, out var index))
                    {
                        var ticket = tickets[index];
                        tickets[index] = ticket with { Satisfaction = result.Sentiment };
                    }
                }

                _logger.LogInformation(
                    "Sentiment analysis complete for {Count} tickets",
                    sentimentResults.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze sentiment for tickets, defaulting to 100");
                
                foreach (var ticketId in ticketIndexMap.Keys)
                {
                    if (ticketIndexMap.TryGetValue(ticketId, out var index))
                    {
                        var ticket = tickets[index];
                        tickets[index] = ticket with { Satisfaction = 100 };
                    }
                }
            }
        }
    }

    // Pagination token structure is now handled by TicketPageToken in ITicketPaginationService
}
