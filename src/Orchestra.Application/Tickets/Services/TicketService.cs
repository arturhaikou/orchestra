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

public class TicketService : ITicketService
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IWorkspaceDataAccess _workspaceDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ITicketProviderFactory _ticketProviderFactory;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly ITicketMappingService _ticketMappingService;
    private readonly IUserDataAccess _userDataAccess;
    private readonly ISummarizationService _summarizationService;
    private readonly ISentimentAnalysisService _sentimentAnalysisService;
    private readonly IAgentDataAccess _agentDataAccess; // FR-004: Required for workspace validation
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ITicketDataAccess ticketDataAccess, 
        IWorkspaceDataAccess workspaceDataAccess, 
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IIntegrationDataAccess integrationDataAccess,
        ITicketProviderFactory ticketProviderFactory,
        ICredentialEncryptionService credentialEncryptionService,
        ITicketMappingService ticketMappingService,
        IUserDataAccess userDataAccess,
        ISummarizationService summarizationService,
        ISentimentAnalysisService sentimentAnalysisService,
        IAgentDataAccess agentDataAccess,
        ILogger<TicketService> logger)
    {
        _ticketDataAccess = ticketDataAccess;
        _workspaceDataAccess = workspaceDataAccess;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _integrationDataAccess = integrationDataAccess;
        _ticketProviderFactory = ticketProviderFactory;
        _credentialEncryptionService = credentialEncryptionService;
        _ticketMappingService = ticketMappingService;
        _userDataAccess = userDataAccess;
        _summarizationService = summarizationService;
        _sentimentAnalysisService = sentimentAnalysisService;
        _agentDataAccess = agentDataAccess;
        _logger = logger;
    }

    public async Task<TicketDto> CreateTicketAsync(Guid userId, CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        // Enforce workspace membership
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, cancellationToken);

        // Validate workspace exists
        var workspace = await _workspaceDataAccess.GetByIdAsync(request.WorkspaceId, cancellationToken);
        if (workspace == null)
        {
            throw new WorkspaceNotFoundException(request.WorkspaceId);
        }

        // Validate status exists
        var status = await _ticketDataAccess.GetStatusByIdAsync(request.StatusId, cancellationToken);
        if (status == null)
        {
            throw new InvalidOperationException($"Status with ID '{request.StatusId}' not found.");
        }

        // Validate priority exists
        var priority = await _ticketDataAccess.GetPriorityByIdAsync(request.PriorityId, cancellationToken);
        if (priority == null)
        {
            throw new InvalidOperationException($"Priority with ID '{request.PriorityId}' not found.");
        }

        // Create ticket
        var ticket = Ticket.Create(
            request.WorkspaceId,
            request.Title,
            request.Description,
            priority.Id,
            status.Id,
            request.Internal);

        // Handle agent/workflow assignment if provided
        if (request.AssignedAgentId.HasValue || request.AssignedWorkflowId.HasValue)
        {
            Guid? agentWorkspaceId = null;
            Guid? workflowWorkspaceId = null;

            // Validate agent workspace consistency if agent is assigned
            if (request.AssignedAgentId.HasValue)
            {
                var agent = await _agentDataAccess.GetByIdAsync(
                    request.AssignedAgentId.Value, 
                    cancellationToken);
                
                if (agent == null)
                {
                    throw new ArgumentException(
                        $"Agent with ID {request.AssignedAgentId.Value} not found.");
                }
                
                agentWorkspaceId = agent.WorkspaceId;
            }

            // Note: Workflow validation skipped until Workflow entity exists
            // workflowWorkspaceId remains null

            // Apply assignments with workspace validation
            ticket.UpdateAssignments(
                request.AssignedAgentId ?? ticket.AssignedAgentId,
                agentWorkspaceId,
                request.AssignedWorkflowId ?? ticket.AssignedWorkflowId,
                workflowWorkspaceId);
        }

        // Add ticket
        await _ticketDataAccess.AddTicketAsync(ticket, cancellationToken);

        // Return DTO
        return new TicketDto(
            Id: ticket.Id.ToString(),
            WorkspaceId: ticket.WorkspaceId,
            Title: ticket.Title,
            Description: ticket.Description,
            Status: status != null ? new TicketStatusDto(status.Id, status.Name, status.Color) : null,
            Priority: priority != null ? new TicketPriorityDto(priority.Id, priority.Name, priority.Color, priority.Value) : null,
            Internal: ticket.IsInternal,
            IntegrationId: null,
            ExternalTicketId: null,
            ExternalUrl: null,
            Source: "INTERNAL",
            AssignedAgentId: ticket.AssignedAgentId,
            AssignedWorkflowId: ticket.AssignedWorkflowId,
            Comments: new List<CommentDto>(),
            Satisfaction: null,
            Summary: null
        );
    }

    public async Task<PaginatedTicketsResponse> GetTicketsAsync(
        Guid workspaceId,
        Guid userId,
        string? pageToken = null,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Parameter validation
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));
        
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        
        // Enforce max page size
        if (pageSize > 100)
            pageSize = 100;
        if (pageSize < 1)
            pageSize = 50;
        
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

        // Parse page token to determine current phase and offset
        var currentToken = new PageToken();
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            try
            {
                var tokenBytes = Convert.FromBase64String(pageToken);
                var tokenJson = Encoding.UTF8.GetString(tokenBytes);
                currentToken = JsonSerializer.Deserialize<PageToken>(tokenJson) ?? new PageToken();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid page token provided, starting from beginning");
                currentToken = new PageToken();
            }
        }

        var resultTickets = new List<TicketDto>();
        var isLastPage = false;
        PageToken? nextToken = null;

        // PHASE 1: Fetch Internal Tickets (pure internal + materialized external)
        if (currentToken.Phase == "internal")
        {
            _logger.LogDebug(
                "Phase 1: Fetching internal tickets (offset: {Offset}, limit: {Limit})",
                currentToken.InternalOffset, pageSize);

            // Fetch paginated internal tickets from database
            var internalTickets = await _ticketDataAccess.GetInternalTicketsByWorkspaceAsync(
                workspaceId,
                currentToken.InternalOffset,
                pageSize,
                cancellationToken);

            // Load status and priority lookups for mapping
            var allStatuses = await _ticketDataAccess.GetAllStatusesAsync(cancellationToken);
            var allPriorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);
            var statusLookup = allStatuses.ToDictionary(s => s.Id, s => s);
            var priorityLookup = allPriorities.ToDictionary(p => p.Id, p => p);

            // Map internal tickets to DTOs
            foreach (var ticket in internalTickets)
            {
                var ticketDto = MapInternalTicketToDto(ticket, statusLookup, priorityLookup);
                resultTickets.Add(ticketDto);
            }

            _logger.LogInformation(
                "Phase 1 complete: Fetched {Count} internal tickets",
                resultTickets.Count);

            // Check if page is full with internal tickets only
            if (resultTickets.Count == pageSize)
            {
                // Page full - prepare next token for more internal tickets
                nextToken = new PageToken
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
                    var externalTickets = await FetchExternalTicketsAsync(
                        trackerIntegrations,
                        remainingSlots,
                        null, // No previous external state
                        cancellationToken);

                    resultTickets.AddRange(externalTickets.Tickets);

                    _logger.LogInformation(
                        "Phase 2 complete: Fetched {Count} external tickets",
                        externalTickets.Tickets.Count);

                    // Determine if more external tickets available
                    if (externalTickets.HasMore)
                    {
                        // More external tickets available - prepare next token for external phase
                        nextToken = new PageToken
                        {
                            Phase = "external",
                            InternalOffset = currentToken.InternalOffset + internalTickets.Count,
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
                var externalTickets = await FetchExternalTicketsAsync(
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
                    nextToken = new PageToken
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

        // Generate next page token
        string? nextPageTokenString = null;
        if (nextToken != null && !isLastPage)
        {
            var tokenJson = JsonSerializer.Serialize(nextToken);
            var tokenBytes = Encoding.UTF8.GetBytes(tokenJson);
            nextPageTokenString = Convert.ToBase64String(tokenBytes);
        }

        // Deduplicate tickets by ID (defensive measure against duplicate materialized tickets)
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
            TotalCount: resultTickets.Count // Dynamic count - only what we've seen
        );
    }

    /// <summary>
    /// Fetches external tickets from multiple providers with round-robin distribution.
    /// </summary>
    private async Task<(List<TicketDto> Tickets, bool HasMore, ExternalPaginationState State)> FetchExternalTicketsAsync(
        List<Integration> trackerIntegrations,
        int slotsToFill,
        ExternalPaginationState? currentState,
        CancellationToken cancellationToken)
    {
        var resultTickets = new List<TicketDto>();
        var state = currentState ?? new ExternalPaginationState();
        
        // Calculate round-robin distribution
        var distribution = CalculateProviderDistribution(trackerIntegrations, slotsToFill);

        // Fetch tickets from each provider according to distribution
        foreach (var (integrationId, ticketCount) in distribution)
        {
            if (ticketCount <= 0) continue;

            var integration = trackerIntegrations.FirstOrDefault(i => i.Id == integrationId);
            if (integration == null)
            {
                _logger.LogWarning(
                    "Integration {IntegrationId} not found or missing provider",
                    integrationId);
                continue;
            }

            try
            {
                var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
                if (provider == null)
                {
                    _logger.LogWarning(
                        "No provider implementation found for {Provider} (Integration: {IntegrationId})",
                        integration.Provider,
                        integrationId);
                    continue;
                }

                // Get provider token from state
                var providerToken = state.ProviderTokens.GetValueOrDefault(integrationId.ToString());

                // Fetch tickets from provider
                var (externalTickets, isLast, nextProviderToken) = await provider.FetchTicketsAsync(
                    integration,
                    state.TotalExternalFetched,
                    ticketCount,
                    providerToken,
                    cancellationToken);

                // Update state with new provider token
                state.ProviderTokens[integrationId.ToString()] = nextProviderToken;
                state.TotalExternalFetched += externalTickets.Count;

                _logger.LogDebug(
                    "Fetched {Count} tickets from {Provider} integration {IntegrationId}",
                    externalTickets.Count,
                    integration.Provider,
                    integrationId);

                // Build materialized ticket lookup
                var materializedTickets = new Dictionary<(Guid, string), Ticket>();
                foreach (var extTicket in externalTickets)
                {
                    var materializedTicket = await _ticketDataAccess.GetTicketByExternalIdAsync(
                        integrationId,
                        extTicket.ExternalTicketId,
                        cancellationToken);

                    if (materializedTicket != null)
                    {
                        materializedTickets[(integrationId, extTicket.ExternalTicketId)] = materializedTicket;
                    }
                }

                // Map external tickets to DTOs and merge with materialized data
                foreach (var extTicket in externalTickets)
                {
                    var compositeId = $"{integrationId}:{extTicket.ExternalTicketId}";
                    
                    // Check if materialized
                    var hasMaterialized = materializedTickets.TryGetValue(
                        (integrationId, extTicket.ExternalTicketId),
                        out var materializedTicket);

                    // For materialized tickets with internal status/priority, use those instead of external
                    TicketStatusDto? status;
                    TicketPriorityDto? priority;

                    if (hasMaterialized && materializedTicket!.StatusId.HasValue)
                    {
                        // Use internal status for materialized tickets
                        var internalStatus = await _ticketDataAccess.GetStatusByIdAsync(
                            materializedTicket.StatusId.Value, 
                            cancellationToken);
                        status = internalStatus != null 
                            ? new TicketStatusDto(internalStatus.Id, internalStatus.Name, internalStatus.Color)
                            : (!string.IsNullOrEmpty(extTicket.StatusName)
                                ? new TicketStatusDto(Guid.Empty, extTicket.StatusName, extTicket.StatusColor ?? "bg-gray-500")
                                : null);
                    }
                    else
                    {
                        // Use external status for non-materialized tickets
                        status = !string.IsNullOrEmpty(extTicket.StatusName)
                            ? new TicketStatusDto(Guid.Empty, extTicket.StatusName, extTicket.StatusColor ?? "bg-gray-500")
                            : null;
                    }

                    if (hasMaterialized && materializedTicket!.PriorityId.HasValue)
                    {
                        // Use internal priority for materialized tickets
                        var internalPriority = await _ticketDataAccess.GetPriorityByIdAsync(
                            materializedTicket.PriorityId.Value, 
                            cancellationToken);
                        priority = internalPriority != null 
                            ? new TicketPriorityDto(internalPriority.Id, internalPriority.Name, internalPriority.Color, internalPriority.Value)
                            : (!string.IsNullOrEmpty(extTicket.PriorityName)
                                ? new TicketPriorityDto(Guid.Empty, extTicket.PriorityName, extTicket.PriorityColor ?? "bg-gray-500", extTicket.PriorityValue)
                                : null);
                    }
                    else
                    {
                        // Use external priority for non-materialized tickets
                        priority = !string.IsNullOrEmpty(extTicket.PriorityName)
                            ? new TicketPriorityDto(Guid.Empty, extTicket.PriorityName, extTicket.PriorityColor ?? "bg-gray-500", extTicket.PriorityValue)
                            : null;
                    }

                    var ticketDto = new TicketDto(
                        Id: compositeId,
                        WorkspaceId: integration.WorkspaceId,
                        Title: extTicket.Title,
                        Description: extTicket.Description,
                        Status: status,
                        Priority: priority,
                        Internal: false,
                        IntegrationId: integrationId,
                        ExternalTicketId: extTicket.ExternalTicketId,
                        ExternalUrl: extTicket.ExternalUrl,
                        Source: integration.Provider.ToString().ToUpperInvariant(),
                        AssignedAgentId: materializedTicket?.AssignedAgentId,
                        AssignedWorkflowId: materializedTicket?.AssignedWorkflowId,
                        Comments: extTicket.Comments,
                        Satisfaction: null,
                        Summary: null
                    );

                    resultTickets.Add(ticketDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to fetch tickets from {Provider} integration {IntegrationId}",
                    integration.Provider,
                    integrationId);
                // Continue with other providers (graceful degradation)
            }
        }

        // Determine if more tickets available from any provider
        // For simplicity, assume more available if we got exactly what we asked for
        var hasMore = resultTickets.Count >= slotsToFill;

        return (resultTickets, hasMore, state);
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
            var parseResult = TicketIdValidator.Parse(ticketId);
            
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
            // Already logged above, re-throw
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

    public async Task<TicketDto> UpdateTicketAsync(
        string ticketId,
        Guid userId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Updating ticket {TicketId} for user {UserId}",
            ticketId, userId);

        // Parameter validation
        if (string.IsNullOrWhiteSpace(ticketId))
            throw new ArgumentException("Ticket ID is required.", nameof(ticketId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        // Validate at least one field is provided
        if (request.StatusId == null && 
            request.PriorityId == null && 
            request.AssignedAgentId == null && 
            request.AssignedWorkflowId == null &&
            string.IsNullOrEmpty(request.Description))
        {
            throw new ArgumentException(
                "At least one field must be provided for update.",
                nameof(request));
        }

        // ID format detection and routing
        var parseResult = TicketIdValidator.Parse(ticketId);
        
        if (parseResult.Type == TicketIdType.External)
        {
            return await UpdateExternalTicketAsync(ticketId, userId, request, cancellationToken);
        }
        else
        {
            return await UpdateInternalTicketAsync(parseResult.InternalId!.Value, userId, request, cancellationToken);
        }
    }

    public async Task<TicketDto> ConvertToExternalAsync(
        string ticketId,
        Guid userId,
        Guid integrationId,
        string issueTypeName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Converting ticket {TicketId} to external for user {UserId} using integration {IntegrationId}",
            ticketId, userId, integrationId);

        // 1. Validate and get internal ticket
        if (!Guid.TryParse(ticketId, out var ticketGuid))
        {
            throw new InvalidOperationException("Only internal tickets can be converted. Ticket ID must be a GUID.");
        }

        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketGuid, cancellationToken);
        if (ticket == null)
        {
            throw new TicketNotFoundException(ticketId);
        }

        if (!ticket.IsInternal)
        {
            throw new InvalidOperationException("Ticket is already external and cannot be converted again.");
        }

        // 2. Verify workspace access
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, 
            ticket.WorkspaceId, 
            cancellationToken);

        // 3. Get and validate integration
        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken);
        if (integration == null)
        {
            throw new IntegrationNotFoundException(integrationId);
        }

        if (integration.Type != IntegrationType.TRACKER)
        {
            throw new InvalidOperationException(
                $"Integration must be a tracker type. Current type: {integration.Type}");
        }

        if (!integration.IsActive)
        {
            throw new InvalidOperationException("Integration is not active.");
        }

        if (integration.WorkspaceId != ticket.WorkspaceId)
        {
            throw new InvalidOperationException(
                "Integration and ticket must belong to the same workspace.");
        }

        // 4. Create external issue via provider
        var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
        if (provider == null)
        {
            throw new InvalidOperationException(
                $"Provider {integration.Provider} is not supported for ticket conversion.");
        }
        
        var result = await provider.CreateIssueAsync(
            integration,
            ticket.Title,
            ticket.Description ?? string.Empty,
            issueTypeName,
            cancellationToken
        );

        _logger.LogInformation(
            "Created external issue {IssueKey} ({IssueUrl}) for ticket {TicketId}",
            result.IssueKey, result.IssueUrl, ticketId);

        // 5. Convert ticket in domain
        ticket.ConvertToExternal(integrationId, result.IssueKey);
        await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

        _logger.LogInformation(
            "Successfully converted ticket {TicketId} to external ticket {ExternalTicketId}",
            ticketId, result.IssueKey);

        // 6. Return updated ticket with composite ID
        var compositeId = $"{integrationId}:{result.IssueKey}";
        return await GetTicketByIdAsync(compositeId, userId, cancellationToken);
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

    // Placeholder methods to be implemented in next tasks
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
        // Note: Materialized external tickets are redirected above, so this only handles pure internal tickets
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
        // Internal tickets always get Satisfaction = 100
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
        var parseResult = TicketIdValidator.Parse(compositeId);
        
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
            AssignedAgentId: assignedAgentId, // From DB if materialized
            AssignedWorkflowId: assignedWorkflowId, // From DB if materialized
            Comments: mergedComments,
            Satisfaction: null,
            Summary: null
        );

        // 7. Calculate sentiment/satisfaction
        ticketDto = await CalculateSentimentForSingleTicketAsync(ticketDto, cancellationToken);

        return ticketDto;
    }

    /// <summary>
    /// Updates an internal ticket with new status, priority, or assignments.
    /// Validates agent workspace consistency before applying changes (FR-004).
    /// </summary>
    /// <param name="ticketId">The internal ticket ID to update</param>
    /// <param name="userId">The user ID making the request</param>
    /// <param name="request">Update request containing optional field changes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated ticket DTO</returns>
    /// <exception cref="ArgumentException">Thrown when agent ID is provided but agent not found</exception>
    /// <exception cref="InvalidWorkspaceAssignmentException">Thrown when agent belongs to different workspace than ticket</exception>
    /// <exception cref="TicketNotFoundException">Thrown when ticket not found</exception>
    private async Task<TicketDto> UpdateInternalTicketAsync(
        Guid ticketId,
        Guid userId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Updating internal ticket {TicketId}",
            ticketId);

        // 1. Load ticket from database
        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
        
        if (ticket == null)
        {
            throw new TicketNotFoundException(ticketId.ToString());
        }

        // 2. Validate workspace authorization
        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId,
            ticket.WorkspaceId,
            cancellationToken);
        
        if (!hasAccess)
        {
            throw new UnauthorizedTicketAccessException(userId, ticketId.ToString());
        }

        // 3. Apply updates via domain methods
        try
        {
            if (request.StatusId.HasValue)
            {
                ticket.UpdateStatus(request.StatusId.Value);
            }

            if (request.PriorityId.HasValue)
            {
                ticket.UpdatePriority(request.PriorityId.Value);
            }

            if (!string.IsNullOrEmpty(request.Description))
            {
                ticket.UpdateDescription(request.Description);
            }

            // FR-004: Application layer validation - fetch agent and extract workspace
            // for domain-level workspace consistency validation
            // Always process assignment updates, even when unassigning (null values)
            Guid? agentWorkspaceId = null;
            Guid? workflowWorkspaceId = null;

            // Fetch agent to validate workspace if a new agent is being assigned (not unassigning)
            if (request.AssignedAgentId.HasValue)
            {
                var agent = await _agentDataAccess.GetByIdAsync(
                    request.AssignedAgentId.Value, 
                    cancellationToken);
                
                // FR-004: Ensure agent exists before proceeding
                if (agent == null)
                {
                    throw new ArgumentException(
                        $"Agent with ID {request.AssignedAgentId.Value} not found.");
                }
                
                // FR-004: Extract workspace ID for domain validation
                agentWorkspaceId = agent.WorkspaceId;
            }

            // Note: Workflow validation skipped until Workflow entity exists
            // workflowWorkspaceId remains null

            // FR-004: Pass workspace IDs to domain for validation
            // Domain will throw InvalidWorkspaceAssignmentException if workspaces don't match
            // Support unassignment by passing null values directly to UpdateAssignments
            ticket.UpdateAssignments(
                request.AssignedAgentId,
                agentWorkspaceId,
                request.AssignedWorkflowId,
                workflowWorkspaceId);
        }
        catch (InvalidOperationException ex)
        {
            // Domain method threw exception (shouldn't happen for internal tickets)
            _logger.LogError(ex,
                "Domain validation failed while updating internal ticket {TicketId}",
                ticketId);
            throw new InvalidTicketOperationException(ex.Message, ex);
        }

        // 4. Persist changes to database
        await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

        _logger.LogInformation(
            "Successfully updated internal ticket {TicketId}",
            ticketId);

        // 5. Fetch and return updated ticket
        return await GetTicketByIdAsync(ticketId.ToString(), userId, cancellationToken);
    }

    /// <summary>
    /// Updates an external ticket's assignments, materializing it if necessary.
    /// Validates agent workspace consistency before applying changes (FR-004).
    /// </summary>
    /// <param name="compositeId">The composite ticket ID in format {integrationId}:{externalTicketId}</param>
    /// <param name="userId">The user ID making the request</param>
    /// <param name="request">Update request containing assignment changes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated or materialized ticket DTO</returns>
    /// <exception cref="ArgumentException">Thrown when agent ID is provided but agent not found</exception>
    /// <exception cref="InvalidWorkspaceAssignmentException">Thrown when agent belongs to different workspace</exception>
    /// <exception cref="InvalidTicketOperationException">Thrown when trying to update unmaterialized ticket without assignments</exception>
    private async Task<TicketDto> UpdateExternalTicketAsync(
        string compositeId,
        Guid userId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Updating external ticket {CompositeId}",
            compositeId);

        // 1. Validate no status/priority updates (external tickets can't update metadata)
        if (request.StatusId.HasValue || request.PriorityId.HasValue)
        {
            throw new InvalidTicketOperationException(
                "Cannot update status or priority of external tickets. " +
                "These fields are managed by the external provider.");
        }

        // 2. Parse composite ID
        var parts = compositeId.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                $"Invalid composite ID format: '{compositeId}'. Expected format: '{{integrationId}}:{{externalTicketId}}'",
                nameof(compositeId));
        }

        var integrationIdString = parts[0];
        var externalTicketId = parts[1];

        if (!Guid.TryParse(integrationIdString, out var integrationId))
        {
            throw new ArgumentException(
                $"Invalid integration ID in composite ID: '{integrationIdString}'",
                nameof(compositeId));
        }

        // 3. Load integration and validate workspace authorization
        var integration = await _integrationDataAccess.GetByIdAsync(
            integrationId,
            cancellationToken);
        
        if (integration == null)
        {
            throw new TicketNotFoundException(compositeId);
        }

        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId,
            integration.WorkspaceId,
            cancellationToken);
        
        if (!hasAccess)
        {
            throw new UnauthorizedTicketAccessException(userId, compositeId);
        }

        // 4. Check if already materialized
        var materializedTicket = await _ticketDataAccess.GetTicketByExternalIdAsync(
            integrationId,
            externalTicketId,
            cancellationToken);

        if (materializedTicket == null)
        {
            // 5. Materialize if assignments provided
            if (request.AssignedAgentId.HasValue || request.AssignedWorkflowId.HasValue)
            {
                _logger.LogInformation(
                    "Materializing external ticket {ExternalTicketId} for integration {IntegrationId}",
                    externalTicketId, integrationId);

                // Fetch external ticket from provider to determine initial priority
                var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
                if (provider == null)
                {
                    throw new InvalidOperationException(
                        $"No provider implementation found for '{integration.Provider}'");
                }

                var externalTicket = await provider.GetTicketByIdAsync(
                    integration,
                    externalTicketId,
                    cancellationToken);

                if (externalTicket == null)
                {
                    throw new TicketNotFoundException(compositeId);
                }

                // Map external priority to internal priority (by name or default to medium)
                var allPriorities = await _ticketDataAccess.GetAllPrioritiesAsync(cancellationToken);
                var mappedPriority = allPriorities
                    .OrderBy(p => Math.Abs(p.Value - (externalTicket.PriorityValue)))
                    .FirstOrDefault();

                if (mappedPriority == null)
                {
                    throw new InvalidOperationException("No priorities found in the system.");
                }

                // Status GUIDs from seeding
                var toDoStatusId = Guid.Parse("66666666-6666-6666-6666-666666666666");

                materializedTicket = Ticket.MaterializeFromExternal(
                    integration.WorkspaceId,
                    integrationId,
                    externalTicketId,
                    statusId: toDoStatusId,
                    priorityId: mappedPriority.Id,
                    assignedAgentId: request.AssignedAgentId,
                    assignedWorkflowId: request.AssignedWorkflowId);

                await _ticketDataAccess.AddTicketAsync(materializedTicket, cancellationToken);

                _logger.LogInformation(
                    "Successfully materialized external ticket {ExternalTicketId}",
                    externalTicketId);
            }
            else
            {
                // No assignments to update and not materialized - nothing to do
                throw new InvalidTicketOperationException(
                    "No assignments provided for unmaterialized external ticket. " +
                    "External tickets must have at least one assignment to be materialized.");
            }
        }
        else
        {
            // 6. Update existing materialized ticket
            _logger.LogDebug(
                "Updating assignments for materialized external ticket {ExternalTicketId}",
                externalTicketId);

            // FR-004: Application layer validation for materialized external tickets
            // Always process assignment updates, even when unassigning (null values)
            Guid? agentWorkspaceId = null;
            Guid? workflowWorkspaceId = null;

            // Fetch agent to validate workspace if a new agent is being assigned (not unassigning)
            if (request.AssignedAgentId.HasValue)
            {
                var agent = await _agentDataAccess.GetByIdAsync(
                    request.AssignedAgentId.Value, 
                    cancellationToken);
                
                // FR-004: Ensure agent exists before proceeding
                if (agent == null)
                {
                    throw new ArgumentException(
                        $"Agent with ID {request.AssignedAgentId.Value} not found.");
                }
                
                // FR-004: Extract workspace ID for domain validation
                agentWorkspaceId = agent.WorkspaceId;
            }

            // Note: Workflow validation skipped until Workflow entity exists
            // workflowWorkspaceId remains null

            // FR-004: Pass workspace IDs to domain for validation
            // InvalidWorkspaceAssignmentException will propagate if validation fails
            // Support unassignment by passing null values directly to UpdateAssignments
            materializedTicket.UpdateAssignments(
                request.AssignedAgentId,
                agentWorkspaceId,
                request.AssignedWorkflowId,
                workflowWorkspaceId);

            await _ticketDataAccess.UpdateTicketAsync(materializedTicket, cancellationToken);
        }

        _logger.LogInformation(
            "Successfully updated external ticket {CompositeId}",
            compositeId);

        // 7. Fetch latest data from provider and merge with DB assignments
        return await GetTicketByIdAsync(compositeId, userId, cancellationToken);
    }

    public async Task DeleteTicketAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate ticket ID format (must be GUID, not composite)
        var parseResult = TicketIdValidator.Parse(ticketId);
        
        if (parseResult.Type != TicketIdType.Internal)
        {
            throw new InvalidTicketOperationException(
                "Cannot delete external tickets. Only internal tickets with GUID format can be deleted.");
        }
        
        var ticketGuid = parseResult.InternalId!.Value;
        
        // 2. Load ticket from database
        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketGuid, cancellationToken);
        
        if (ticket == null)
        {
            _logger.LogWarning("Ticket {TicketId} not found for deletion by user {UserId}", ticketId, userId);
            throw new TicketNotFoundException(ticketId);
        }
        
        // 3. Validate workspace access
        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId,
            ticket.WorkspaceId,
            cancellationToken);
        
        if (!hasAccess)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete ticket {TicketId} from workspace {WorkspaceId} without authorization",
                userId, ticketId, ticket.WorkspaceId);
            throw new UnauthorizedTicketAccessException(userId, ticketId);
        }
        
        // 4. Check if ticket is deletable using domain method
        if (!ticket.CanDelete())
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete external ticket {TicketId} (IntegrationId: {IntegrationId})",
                userId, ticketId, ticket.IntegrationId);
            throw new InvalidTicketOperationException(
                "Cannot delete external tickets. External tickets must be deleted in their source system.");
        }
        
        // 5. Delete ticket (comments cascade deleted automatically)
        await _ticketDataAccess.DeleteTicketAsync(ticketGuid, cancellationToken);
        
        _logger.LogInformation(
            "User {UserId} successfully deleted ticket {TicketId} from workspace {WorkspaceId}",
            userId, ticketId, ticket.WorkspaceId);
    }

    public async Task<CommentDto> AddCommentAsync(
        string ticketId,
        Guid userId,
        AddCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate content is not empty
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Comment content cannot be empty.", nameof(request));
        }

        // Parse ticket ID to determine routing (composite key = external, GUID = internal)
        var parseResult = TicketIdValidator.Parse(ticketId);
        
        if (parseResult.Type == TicketIdType.Internal)
        {
            // Internal ticket - persist to database
            return await AddCommentToInternalTicketAsync(ticketId, userId, request, cancellationToken);
        }
        else
        {
            // External ticket (composite ID) - route to provider API
            return await AddCommentToExternalTicketAsync(ticketId, userId, request, cancellationToken);
        }
    }

    private async Task<CommentDto> AddCommentToInternalTicketAsync(
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
            comment.Content);
    }

    private async Task<CommentDto> AddCommentToExternalTicketAsync(
        string ticketId,
        Guid userId,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        // Parse composite ID to extract integration and external ticket IDs
        var parseResult = TicketIdValidator.Parse(ticketId);
        
        if (parseResult.Type != TicketIdType.External)
        {
            throw new ArgumentException(
                $"Expected external ticket ID in composite format, but received: '{ticketId}'",
                nameof(ticketId));
        }
        
        var integrationId = parseResult.IntegrationId!.Value;
        var externalTicketId = parseResult.ExternalTicketId!;

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

    public async Task<TicketDto> GenerateSummaryAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch the ticket (handles both internal and external tickets)
        var ticketDto = await GetTicketByIdAsync(ticketId, userId, cancellationToken);

        // 2. Build content string for summarization
        var contentBuilder = new StringBuilder();
        
        // Add title
        contentBuilder.AppendLine($"Title: {ticketDto.Title}");
        contentBuilder.AppendLine();
        
        // Add description
        contentBuilder.AppendLine("Description:");
        contentBuilder.AppendLine(ticketDto.Description);
        contentBuilder.AppendLine();
        
        // Add comments if any exist
        if (ticketDto.Comments != null && ticketDto.Comments.Any())
        {
            contentBuilder.AppendLine("Comments:");
            foreach (var comment in ticketDto.Comments)
            {
                contentBuilder.AppendLine($"- {comment.Author}: {comment.Content}");
            }
        }

        var content = contentBuilder.ToString();

        // 3. Generate summary using AI service
        string summary;
        try
        {
            summary = await _summarizationService.GenerateSummaryAsync(content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to generate summary for ticket {TicketId} by user {UserId}", 
                ticketId, userId);
            throw;
        }

        // 4. Return ticket DTO with summary populated
        return ticketDto with { Summary = summary };
    }

    /// <summary>
    /// Calculates round-robin distribution of remaining page slots across multiple provider integrations.
    /// Ensures fair distribution when fetching external tickets from multiple sources.
    /// </summary>
    /// <param name="integrations">List of TRACKER integrations to distribute across</param>
    /// <param name="remainingSlots">Number of tickets remaining to fill the page</param>
    /// <returns>Dictionary mapping integration ID to number of tickets to fetch from that provider</returns>
    private Dictionary<Guid, int> CalculateProviderDistribution(
        List<Integration> integrations,
        int remainingSlots)
    {
        var distribution = new Dictionary<Guid, int>();
        
        if (integrations.Count == 0 || remainingSlots <= 0)
        {
            return distribution;
        }

        // Calculate base slots per provider and remainder
        var baseSlots = remainingSlots / integrations.Count;
        var remainder = remainingSlots % integrations.Count;

        // Distribute base slots to all providers
        foreach (var integration in integrations)
        {
            distribution[integration.Id] = baseSlots;
        }

        // Distribute remainder slots to first N providers
        for (int i = 0; i < remainder; i++)
        {
            distribution[integrations[i].Id]++;
        }

        return distribution;
    }

    /// <summary>
    /// Internal structure for pagination token.
    /// Serialized to base64-encoded JSON for opaque cursor-based pagination.
    /// Supports phased pagination: internal tickets first, then external tickets.
    /// </summary>
    private class PageToken
    {
        /// <summary>
        /// Current pagination phase: "internal" or "external"
        /// </summary>
        public string Phase { get; set; } = "internal";
        
        /// <summary>
        /// Offset for internal ticket pagination (0-based)
        /// </summary>
        public int InternalOffset { get; set; } = 0;
        
        /// <summary>
        /// State for external ticket pagination across multiple providers
        /// </summary>
        public ExternalPaginationState? ExternalState { get; set; }
    }

    /// <summary>
    /// Tracks pagination state when fetching external tickets from multiple providers.
    /// </summary>
    private class ExternalPaginationState
    {
        /// <summary>
        /// Index of the current provider being fetched (0-based)
        /// </summary>
        public int CurrentProviderIndex { get; set; } = 0;
        
        /// <summary>
        /// Provider-specific continuation tokens mapped by integration ID
        /// </summary>
        public Dictionary<string, string?> ProviderTokens { get; set; } = new();
        
        /// <summary>
        /// Total number of external tickets fetched so far across all providers
        /// </summary>
        public int TotalExternalFetched { get; set; } = 0;
    }

    /// <summary>
    /// Calculates sentiment/satisfaction scores for a list of tickets.
    /// - Internal tickets always get Satisfaction = 100
    /// - Tickets without comments get Satisfaction = 100
    /// - External tickets with comments are analyzed by the sentiment service
    /// </summary>
    private async Task CalculateSentimentForTicketsAsync(
        List<TicketDto> tickets,
        CancellationToken cancellationToken)
    {
        if (tickets == null || tickets.Count == 0)
            return;

        var ticketsToAnalyze = new List<TicketSentimentRequest>();
        var ticketIndexMap = new Dictionary<string, int>(); // Map ticketId to index in tickets list

        for (int i = 0; i < tickets.Count; i++)
        {
            var ticket = tickets[i];

            // Pure internal tickets always get 100
            if (ticket.Internal && ticket.IntegrationId == null)
            {
                tickets[i] = ticket with { Satisfaction = 100 };
                continue;
            }

            // Tickets without comments get 100
            if (ticket.Comments == null || ticket.Comments.Count == 0)
            {
                tickets[i] = ticket with { Satisfaction = 100 };
                continue;
            }

            // External tickets with comments need sentiment analysis
            var commentContents = ticket.Comments
                .Select(c => c.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (commentContents.Count > 0)
            {
                ticketsToAnalyze.Add(new TicketSentimentRequest(
                    ticket.Id,
                    commentContents
                ));
                ticketIndexMap[ticket.Id] = i;
            }
            else
            {
                // No valid comment content
                tickets[i] = ticket with { Satisfaction = 100 };
            }
        }

        // Analyze sentiment for external tickets with comments
        if (ticketsToAnalyze.Count > 0)
        {
            try
            {
                var sentimentResults = await _sentimentAnalysisService.AnalyzeBatchSentimentAsync(
                    ticketsToAnalyze,
                    cancellationToken);

                // Map results back to tickets
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
                
                // Default to 100 on error
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

    /// <summary>
    /// Calculates sentiment/satisfaction score for a single ticket.
    /// - Internal tickets always get Satisfaction = 100
    /// - Tickets without comments get Satisfaction = 100
    /// - External tickets with comments are analyzed by the sentiment service
    /// </summary>
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

            // No result returned, default to 100
            return ticket with { Satisfaction = 100 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sentiment for ticket {TicketId}, defaulting to 100", ticket.Id);
            return ticket with { Satisfaction = 100 };
        }
    }
}