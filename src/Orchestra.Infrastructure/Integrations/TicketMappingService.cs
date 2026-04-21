using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Orchestra.Infrastructure.Integrations;

/// <summary>
/// Service for mapping external provider data to internal display formats and mapping entities to DTOs.
/// </summary>
public class TicketMappingService : ITicketMappingService
{
    private readonly ILogger<TicketMappingService> _logger;

    public TicketMappingService(ILogger<TicketMappingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Maps external status name to display string with fallback.
    /// </summary>
    public string MapStatusToDisplay(string externalStatus, ProviderType providerType)
    {
        if (string.IsNullOrWhiteSpace(externalStatus))
        {
            _logger.LogWarning("External status is null or whitespace, using fallback 'To Do'");
            return "To Do";
        }

        return providerType switch
        {
            ProviderType.JIRA => MapJiraStatus(externalStatus),
            _ => externalStatus
        };
    }

    private string MapJiraStatus(string jiraStatus)
    {
        var normalizedStatus = jiraStatus.ToLowerInvariant();

        switch (normalizedStatus)
        {
            case "to do":
            case "backlog":
            case "open":
                return "To Do";
            case "in progress":
            case "in review":
            case "review":
                return "InProgress";
            case "done":
            case "closed":
            case "resolved":
            case "complete":
                return "Completed";
            case "new":
                return "New";
            default:
                _logger.LogWarning("Unknown Jira status '{JiraStatus}', using fallback 'To Do'", jiraStatus);
                return "To Do";
        }
    }

    private string MapJiraPriority(string jiraPriority)
    {
        var normalizedPriority = jiraPriority.ToLowerInvariant();

        switch (normalizedPriority)
        {
            case "highest":
            case "blocker":
                return "Critical";
            case "high":
                return "High";
            case "medium":
            case "normal":
                return "Medium";
            case "low":
            case "lowest":
            case "trivial":
                return "Low";
            default:
                _logger.LogWarning("Unknown Jira priority '{JiraPriority}', using fallback 'Medium'", jiraPriority);
                return "Medium";
        }
    }

    /// <summary>
    /// Maps external priority name to display string with fallback.
    /// </summary>
    public string MapPriorityToDisplay(string externalPriority, ProviderType providerType)
    {
        if (string.IsNullOrWhiteSpace(externalPriority))
        {
            _logger.LogWarning("External priority is null or whitespace, using fallback 'Medium'");
            return "Medium";
        }

        return providerType switch
        {
            ProviderType.JIRA => MapJiraPriority(externalPriority),
            _ => externalPriority
        };
    }

    /// <summary>
    /// Constructs external ticket URL from integration base URL and ticket ID.
    /// </summary>
    public string BuildExternalUrl(Integration integration, string externalTicketId)
    {
        var baseUrl = integration.Url?.TrimEnd('/') ?? string.Empty;

        return integration.Provider switch
        {
            ProviderType.JIRA => $"{baseUrl}/browse/{externalTicketId}",
            ProviderType.AZURE_DEVOPS => $"{baseUrl}/_workitems/edit/{externalTicketId}",
            _ => $"{baseUrl}/ticket/{externalTicketId}"
        };
    }

    /// <summary>
    /// Maps an internal ticket entity to TicketDto for display.
    /// Extracted logic from TicketService.MapInternalTicketToDto (no behavioral changes).
    /// Handles composite ID generation for materialized external tickets.
    /// </summary>
    public TicketDto MapInternalTicketToDto(
        Ticket ticket,
        Dictionary<Guid, TicketStatus> statusLookup,
        Dictionary<Guid, TicketPriority> priorityLookup,
        IEnumerable<TicketComment> comments)
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

        // Map pre-fetched comments (navigation property removed — comments passed explicitly)
        var commentDtos = comments
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

    /// <summary>
    /// Maps an external ticket from provider to TicketDto with optional materialized data merging.
    /// Extracted logic from TicketService.FetchAndMergeExternalTicketAsync status/priority merge section.
    /// </summary>
    public async Task<TicketDto> MapExternalTicketToDtoAsync(
        Integration integration,
        ExternalTicketDto externalTicket,
        Ticket? materializedTicket,
        Dictionary<Guid, TicketStatus> statusLookup,
        Dictionary<Guid, TicketPriority> priorityLookup,
        IEnumerable<TicketComment>? materializedComments = null)
    {
        var compositeId = $"{integration.Id}:{externalTicket.ExternalTicketId}";

        // Merge assignments and status/priority if materialized
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
            if (materializedTicket.StatusId.HasValue && statusLookup.ContainsKey(materializedTicket.StatusId.Value))
            {
                var internalStatus = statusLookup[materializedTicket.StatusId.Value];
                status = new TicketStatusDto(internalStatus.Id, internalStatus.Name, internalStatus.Color);
            }

            if (materializedTicket.PriorityId.HasValue && priorityLookup.ContainsKey(materializedTicket.PriorityId.Value))
            {
                var internalPriority = priorityLookup[materializedTicket.PriorityId.Value];
                priority = new TicketPriorityDto(
                    internalPriority.Id,
                    internalPriority.Name,
                    internalPriority.Color,
                    internalPriority.Value);
            }

            // Explicitly fetch internal comments for materialized ticket (navigation property removed)
            var internalCommentDtos = (materializedComments ?? [])
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
            "Mapped external ticket {CompositeId} with materialization={IsMaterialized}",
            compositeId, materializedTicket != null);

        // Return TicketDto in external format
        return await Task.FromResult(new TicketDto(
            Id: compositeId, // Composite ID format
            WorkspaceId: integration.WorkspaceId,
            Title: externalTicket.Title,
            Description: externalTicket.Description,
            Status: status,
            Priority: priority,
            Internal: false,
            IntegrationId: integration.Id,
            ExternalTicketId: externalTicket.ExternalTicketId,
            ExternalUrl: BuildExternalUrl(integration, externalTicket.ExternalTicketId),
            Source: integration.Provider.ToString().ToUpperInvariant(),
            AssignedAgentId: assignedAgentId, // From DB if materialized
            AssignedWorkflowId: assignedWorkflowId, // From DB if materialized
            Comments: mergedComments,
            Satisfaction: null,
            Summary: null
        ));
    }
}