namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Represents a ticket fetched from an external provider (Jira, Azure DevOps, etc.)
/// before merging with internal database records.
/// </summary>
/// <remarks>
/// This DTO does not include database-specific fields like GUID IDs or assignments.
/// External status and priority are represented as display strings, not GUID references.
/// IntegrationId is included to support composite ID generation (integrationId:externalTicketId).
/// </remarks>
public record ExternalTicketDto(
    Guid IntegrationId,
    string ExternalTicketId,
    string Title,
    string Description,
    string StatusName,
    string StatusColor,
    string PriorityName,
    string PriorityColor,
    int PriorityValue,
    string ExternalUrl,
    List<CommentDto> Comments
);