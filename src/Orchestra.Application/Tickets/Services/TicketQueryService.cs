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
        // Parameter validation (delegated to validation service or utility)
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        pageSize = _ticketPaginationService.NormalizePageSize(pageSize);
        
        // Validate workspace access
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

        // Parse page token to determine current phase and offset (delegated to pagination service)
        var currentToken = _ticketPaginationService.ParsePageToken(pageToken);

        var resultTickets = new List<TicketDto>();
        var isLastPage = false;
        TicketPageToken? nextToken = null;

        // PHASE 1: Fetch only pure internal tickets (no externalId)
        if (currentToken.Phase == "internal")
        {
            _logger.LogDebug(
                "Phase 1: Fetching pure internal tickets (offset: {Offset}, limit: {Limit})",
                currentToken.InternalOffset, pageSize);

            // Fetch paginated pure internal tickets from database (externalId == null)
            var internalTickets = await _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(
                workspaceId,
                currentToken.InternalOffset,
                pageSize,
                cancellationToken);

            // Filter to only pure internal tickets (no IntegrationId, no ExternalTicketId)
            var pureInternalTickets = internalTickets
                .Where(t => t.IntegrationId == null && string.IsNullOrEmpty(t.ExternalTicketId))
                .ToList();

            // Load status and priority lookups for mapping
            var allStatuses = await _ticketDataAccess.GetAllStatusesAsync(cancellationToken);
            var allPriorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);
            var statusLookup = allStatuses.ToDictionary(s => s.Id, s => s);
            var priorityLookup = allPriorities.ToDictionary(p => p.Id, p => p);

            // Map pure internal tickets to DTOs
            foreach (var ticket in pureInternalTickets)
            {
                var ticketDto = MapInternalTicketToDto(ticket, statusLookup, priorityLookup);
                resultTickets.Add(ticketDto);
            }

            _logger.LogInformation(
                "Phase 1 complete: Fetched {Count} pure internal tickets",
                resultTickets.Count);

            // Check if page is full with internal tickets only
            if (resultTickets.Count == pageSize)
            {
                // Page full - prepare next token for more internal tickets
                nextToken = new TicketPageToken
                {
                    Phase = "internal",
                    InternalOffset = currentToken.InternalOffset + pageSize
                };
                isLastPage = false;
            }
            else
            {
                // Not enough internal tickets to fill page - transition to external phase
                var remainingSlots = pageSize - resultTickets.Count;

                _logger.LogDebug(
                    "Phase 1 incomplete ({Count}/{PageSize}). Transitioning to Phase 2 with {Remaining} remaining slots",
                    resultTickets.Count, pageSize, remainingSlots);

                // Check if there are external integrations
                var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(
                    workspaceId,
                    cancellationToken);

                var trackerIntegrations = integrations
                    .Where(i => i.Type == IntegrationType.TRACKER)
                    .ToList();

                if (trackerIntegrations.Any() && remainingSlots > 0)
                {
                    // Fetch external tickets to fill remaining slots
                    var externalTickets = await _externalFetchingService.FetchExternalTicketsAsync(
                        trackerIntegrations,
                        remainingSlots,
                        null, // No previous external state
                        cancellationToken);

                    // For each external ticket, check for materialized ticket and return only if matched
                    foreach (var extTicket in externalTickets.Tickets)
                    {
                        if (!string.IsNullOrEmpty(extTicket.ExternalTicketId) && extTicket.IntegrationId != null)
                        {
                            var materialized = await _ticketDataAccess.GetTicketByExternalIdAsync(
                                extTicket.IntegrationId.Value,
                                extTicket.ExternalTicketId,
                                cancellationToken);
                            if (materialized != null)
                            {
                                // Map materialized ticket to DTO
                                var ticketDto = MapInternalTicketToDto(materialized, statusLookup, priorityLookup);
                                resultTickets.Add(ticketDto);
                            }
                            else
                            {
                                // Return external ticket as-is
                                resultTickets.Add(extTicket);
                            }
                        }
                        else
                        {
                            // Return external ticket as-is
                            resultTickets.Add(extTicket);
                        }
                    }

                    _logger.LogInformation(
                        "Phase 2 complete: Fetched {Count} external/materialized tickets",
                        externalTickets.Tickets.Count);

                    // Determine if more external tickets available
                    if (externalTickets.HasMore)
                    {
                        // More external tickets available - prepare next token for external phase
                        nextToken = new TicketPageToken
                        {
                            Phase = "external",
                            InternalOffset = currentToken.InternalOffset + resultTickets.Count,
                            ExternalState = externalTickets.State
                        };
                        isLastPage = false;
                    }
                    else
                    {
                        // No more tickets (internal or external)
                        isLastPage = true;
                    }
                }
                else
                {
                    // No external integrations or no remaining slots - we're done
                    isLastPage = true;
                }
            }
        }
        // PHASE 2: Fetch External Tickets Only
        else if (currentToken.Phase == "external")
        {
            _logger.LogDebug(
                "Phase 2: Fetching external tickets (limit: {Limit})",
                pageSize);

            // Load integrations
            var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(
                workspaceId,
                cancellationToken);

            var trackerIntegrations = integrations
                .Where(i => i.Type == IntegrationType.TRACKER)
                .ToList();

            if (trackerIntegrations.Any())
            {
                // Fetch external tickets using saved state
                var externalTickets = await _externalFetchingService.FetchExternalTicketsAsync(
                    trackerIntegrations,
                    pageSize,
                    currentToken.ExternalState,
                    cancellationToken);

                resultTickets.AddRange(externalTickets.Tickets);

                _logger.LogInformation(
                    "Phase 2 complete: Fetched {Count} external tickets",
                    resultTickets.Count);

                // Determine if more external tickets available
                if (externalTickets.HasMore)
                {
                    nextToken = new TicketPageToken
                    {
                        Phase = "external",
                        InternalOffset = currentToken.InternalOffset,
                        ExternalState = externalTickets.State
                    };
                    isLastPage = false;
                }
                else
                {
                    isLastPage = true;
                }
            }
            else
            {
                // No external integrations - we're done
                isLastPage = true;
            }
        }

        // Generate next page token (delegated to pagination service)
        string? nextPageTokenString = null;
        if (nextToken != null && !isLastPage)
        {
            nextPageTokenString = _ticketPaginationService.SerializePageToken(nextToken);
        }

        // Deduplicate tickets by ID (defensive measure)
        resultTickets = resultTickets
            .GroupBy(t => t.Id)
            .Select(g => g.First())
            .ToList();

        _logger.LogInformation(
            "GetTicketsAsync complete: Returned {Count} tickets (after deduplication), IsLast: {IsLast}",
            resultTickets.Count, isLastPage);

        // Calculate sentiment/satisfaction for all tickets
        await CalculateSentimentForTicketsAsync(resultTickets, cancellationToken);

        // Return paginated response with dynamic count
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

    private async Task<TicketDto> FetchAndMergeExternalTicketAsync(
        Integration integration,
        string externalTicketId,
        string compositeId,
        CancellationToken cancellationToken)
    {
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
            externalTicketId, integration.Provider);

        ExternalTicketDto? externalTicket;
        try
        {
            externalTicket = await provider.GetTicketByIdAsync(
                integration,
                externalTicketId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to fetch ticket {ExternalTicketId} from provider {Provider}",
                externalTicketId, integration.Provider);
            throw new TicketNotFoundException(compositeId);
        }

        if (externalTicket == null)
        {
            throw new TicketNotFoundException(compositeId);
        }

        // 4. Check for materialized DB record
        var materializedTicket = await _ticketDataAccess.GetTicketByExternalIdAsync(
            integration.Id,
            externalTicketId,
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
