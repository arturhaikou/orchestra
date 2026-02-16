using System.ComponentModel.DataAnnotations;

namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Request to convert an internal ticket to an external tracker ticket.
/// </summary>
/// <param name="IntegrationId">The tracker integration ID to create the ticket in.</param>
/// <param name="IssueTypeName">The issue type name (e.g., "Task", "Story", "Bug", "Epic").</param>
public record ConvertTicketRequest(
    [Required] Guid IntegrationId,
    [Required] string IssueTypeName
);
