using System.Collections.Generic;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.Common;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Orchestra.Application.Tickets.Services;

public class TicketCommandService : ITicketCommandService
{
    private readonly ITicketDataAccess _ticketDataAccess;
    private readonly IWorkspaceDataAccess _workspaceDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ITicketProviderFactory _ticketProviderFactory;
    private readonly ITicketIdParsingService _ticketIdParsingService;
    private readonly ITicketAssignmentValidationService _ticketAssignmentValidationService;
    private readonly IUserDataAccess _userDataAccess;
    private readonly ITicketMaterializationService _materializationService;
    private readonly ITicketQueryService _queryService;
    private readonly ILogger<TicketCommandService> _logger;

    public TicketCommandService(
        ITicketDataAccess ticketDataAccess,
        IWorkspaceDataAccess workspaceDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService,
        IIntegrationDataAccess integrationDataAccess,
        ITicketProviderFactory ticketProviderFactory,
        ITicketIdParsingService ticketIdParsingService,
        ITicketAssignmentValidationService ticketAssignmentValidationService,
        IUserDataAccess userDataAccess,
        ITicketMaterializationService materializationService,
        ITicketQueryService queryService,
        ILogger<TicketCommandService> logger)
    {
        _ticketDataAccess = ticketDataAccess;
        _workspaceDataAccess = workspaceDataAccess;
        _workspaceAuthorizationService = workspaceAuthorizationService;
        _integrationDataAccess = integrationDataAccess;
        _ticketProviderFactory = ticketProviderFactory;
        _ticketIdParsingService = ticketIdParsingService;
        _ticketAssignmentValidationService = ticketAssignmentValidationService;
        _userDataAccess = userDataAccess;
        _materializationService = materializationService;
        _queryService = queryService;
        _logger = logger;
    }

    public async Task<TicketDto> CreateTicketAsync(
        Guid userId,
        CreateTicketRequest request,
        CancellationToken cancellationToken = default)
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
            // Validate agent workspace consistency (FR-004)
            var agentWorkspaceId = await _ticketAssignmentValidationService.ValidateAndGetAgentWorkspaceAsync(
                request.AssignedAgentId, 
                cancellationToken);

            // Validate workflow workspace consistency (FR-004)
            var workflowWorkspaceId = await _ticketAssignmentValidationService.ValidateAndGetWorkflowWorkspaceAsync(
                request.AssignedWorkflowId, 
                cancellationToken);

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
            Satisfaction: ticket.IsInternal ? 100 : (int?)null,
            Summary: null
        );
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

        if (string.IsNullOrWhiteSpace(ticketId))
            throw new ArgumentException("Ticket ID is required.", nameof(ticketId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

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

        var parseResult = _ticketIdParsingService.Parse(ticketId);
        
        if (parseResult.Type == TicketIdType.External)
        {
            return await UpdateExternalTicketAsync(parseResult, userId, request, cancellationToken);
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

        // Verify workspace access
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(
            userId, 
            ticket.WorkspaceId, 
            cancellationToken);

        // Get and validate integration
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

        // Create external issue via provider
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

        // Convert ticket in domain
        ticket.ConvertToExternal(integrationId, result.IssueKey);
        await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

        _logger.LogInformation(
            "Successfully converted ticket {TicketId} to external ticket {ExternalTicketId}",
            ticketId, result.IssueKey);

        // Return updated ticket with composite ID
        var compositeId = _ticketIdParsingService.BuildCompositeId(integrationId, result.IssueKey);
        return await _queryService.GetTicketByIdAsync(compositeId, userId, cancellationToken);
    }

    public async Task DeleteTicketAsync(
        string ticketId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var parseResult = _ticketIdParsingService.Parse(ticketId);
        
        if (parseResult.Type != TicketIdType.Internal)
        {
            throw new InvalidTicketOperationException(
                "Cannot delete external tickets. Only internal tickets with GUID format can be deleted.");
        }
        
        var ticketGuid = parseResult.InternalId!.Value;
        
        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketGuid, cancellationToken);
        
        if (ticket == null)
        {
            _logger.LogWarning("Ticket {TicketId} not found for deletion by user {UserId}", ticketId, userId);
            throw new TicketNotFoundException(ticketId);
        }
        
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
        
        if (!ticket.CanDelete())
        {
            _logger.LogWarning(
                "User {UserId} attempted to delete external ticket {TicketId} (IntegrationId: {IntegrationId})",
                userId, ticketId, ticket.IntegrationId);
            throw new InvalidTicketOperationException(
                "Cannot delete external tickets. External tickets must be deleted in their source system.");
        }
        
        await _ticketDataAccess.DeleteTicketAsync(ticketGuid, cancellationToken);
        
        _logger.LogInformation(
            "User {UserId} successfully deleted ticket {TicketId} from workspace {WorkspaceId}",
            userId, ticketId, ticket.WorkspaceId);
    }


    private async Task<TicketDto> UpdateInternalTicketAsync(
        Guid ticketId,
        Guid userId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Updating internal ticket {TicketId}", ticketId);

        var ticket = await _ticketDataAccess.GetTicketByIdAsync(ticketId, cancellationToken);
        
        if (ticket == null)
        {
            throw new TicketNotFoundException(ticketId.ToString());
        }

        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId,
            ticket.WorkspaceId,
            cancellationToken);
        
        if (!hasAccess)
        {
            throw new UnauthorizedTicketAccessException(userId, ticketId.ToString());
        }

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

            var agentWorkspaceId = await _ticketAssignmentValidationService.ValidateAndGetAgentWorkspaceAsync(
                request.AssignedAgentId, 
                cancellationToken);

            var workflowWorkspaceId = await _ticketAssignmentValidationService.ValidateAndGetWorkflowWorkspaceAsync(
                request.AssignedWorkflowId, 
                cancellationToken);

            ticket.UpdateAssignments(
                request.AssignedAgentId,
                agentWorkspaceId,
                request.AssignedWorkflowId,
                workflowWorkspaceId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Domain validation failed while updating internal ticket {TicketId}",
                ticketId);
            throw new InvalidTicketOperationException(ex.Message, ex);
        }

        await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);

        _logger.LogInformation(
            "Successfully updated internal ticket {TicketId}",
            ticketId);

        return await _queryService.GetTicketByIdAsync(ticketId.ToString(), userId, cancellationToken);
    }

    private async Task<TicketDto> UpdateExternalTicketAsync(
        TicketIdParseResult parseResult,
        Guid userId,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var integrationId = parseResult.IntegrationId!.Value;
        var externalTicketId = parseResult.ExternalTicketId!;
        var compositeId = _ticketIdParsingService.BuildCompositeId(integrationId, externalTicketId);

        _logger.LogDebug("Updating external ticket {CompositeId}", compositeId);

        var integration = await _integrationDataAccess.GetByIdAsync(integrationId, cancellationToken);
        if (integration == null)
        {
            throw new TicketNotFoundException(compositeId);
        }

        var hasAccess = await _workspaceAuthorizationService.IsMemberAsync(
            userId, integration.WorkspaceId, cancellationToken);
        if (!hasAccess)
        {
            throw new UnauthorizedTicketAccessException(userId, compositeId);
        }

        var materializedTicket = await _ticketDataAccess.GetTicketByExternalIdAsync(
            integrationId, externalTicketId, cancellationToken);

        if (materializedTicket == null)
        {
            if (request.StatusId.HasValue || request.PriorityId.HasValue)
            {
                throw new InvalidTicketOperationException(
                    "Cannot update status or priority on unmaterialized external tickets. " +
                    "External tickets must be materialized (assigned to an agent or workflow) before status/priority can be managed.");
            }

            if (request.AssignedAgentId.HasValue || request.AssignedWorkflowId.HasValue)
            {
                _logger.LogInformation(
                    "Materializing external ticket {ExternalTicketId} for integration {IntegrationId}",
                    externalTicketId, integrationId);

                await MaterializeExternalTicketAsync(
                    integration, integrationId, externalTicketId,
                    request.AssignedAgentId, request.AssignedWorkflowId,
                    cancellationToken);

                _logger.LogInformation(
                    "Successfully materialized external ticket {ExternalTicketId}",
                    externalTicketId);
            }
            else
            {
                throw new InvalidTicketOperationException(
                    "No assignments provided for unmaterialized external ticket. " +
                    "External tickets must have at least one assignment to be materialized.");
            }
        }
        else
        {
            _logger.LogDebug(
                "Updating assignments for materialized external ticket {ExternalTicketId}",
                externalTicketId);

            try
            {
                await UpdateMaterializedExternalTicketAsync(materializedTicket, request, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex,
                    "Domain validation failed while updating materialized external ticket {ExternalTicketId}",
                    externalTicketId);
                throw new InvalidTicketOperationException(ex.Message, ex);
            }
        }

        _logger.LogInformation("Successfully updated external ticket {CompositeId}", compositeId);

        return await _queryService.GetTicketByIdAsync(compositeId, userId, cancellationToken);
    }

    private async Task MaterializeExternalTicketAsync(
        Integration integration,
        Guid integrationId,
        string externalTicketId,
        Guid? assignedAgentId,
        Guid? assignedWorkflowId,
        CancellationToken cancellationToken)
    {
        var provider = _ticketProviderFactory.CreateProvider(integration.Provider);
        if (provider == null)
        {
            throw new InvalidOperationException(
                $"No provider implementation found for '{integration.Provider}'");
        }

        var externalTicket = await provider.GetTicketByIdAsync(integration, externalTicketId, cancellationToken);
        if (externalTicket == null)
        {
            throw new TicketNotFoundException(_ticketIdParsingService.BuildCompositeId(integrationId, externalTicketId));
        }

        var materializedTicket = await _materializationService.MaterializeFromExternalAsync(
            integrationId, externalTicketId, integration.WorkspaceId,
            externalTicket, assignedAgentId, assignedWorkflowId,
            cancellationToken);

        await _ticketDataAccess.AddTicketAsync(materializedTicket, cancellationToken);
    }

    private async Task UpdateMaterializedExternalTicketAsync(
        Ticket ticket,
        UpdateTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StatusId.HasValue)
        {
            var status = await _ticketDataAccess.GetStatusByIdAsync(request.StatusId.Value, cancellationToken);
            if (status == null)
            {
                throw new InvalidOperationException($"Status with ID '{request.StatusId.Value}' not found.");
            }
            ticket.UpdateStatus(request.StatusId.Value);
        }

        if (request.PriorityId.HasValue)
        {
            var priority = await _ticketDataAccess.GetPriorityByIdAsync(request.PriorityId.Value, cancellationToken);
            if (priority == null)
            {
                throw new InvalidOperationException($"Priority with ID '{request.PriorityId.Value}' not found.");
            }
            ticket.UpdatePriority(request.PriorityId.Value);
        }

        var agentWorkspaceId = await _ticketAssignmentValidationService.ValidateAndGetAgentWorkspaceAsync(
            request.AssignedAgentId, cancellationToken);

        var workflowWorkspaceId = await _ticketAssignmentValidationService.ValidateAndGetWorkflowWorkspaceAsync(
            request.AssignedWorkflowId, cancellationToken);

        ticket.UpdateAssignments(
            request.AssignedAgentId, agentWorkspaceId,
            request.AssignedWorkflowId, workflowWorkspaceId);

        await _ticketDataAccess.UpdateTicketAsync(ticket, cancellationToken);
    }
}
