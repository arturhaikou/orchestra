namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Flat projection DTO returned by <c>GetInternalTicketsByWorkspaceAsync</c>.
/// Priority fields are resolved via an explicit LEFT JOIN on TicketPriorities.
/// IntegrationName is resolved via an explicit LEFT JOIN on Integrations.
/// CommentCount is a correlated scalar sub-query — no comment objects are loaded.
/// </summary>
public class InternalTicketListDto
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Status — ID only; resolved to TicketStatusDto by the service layer lookup
    public Guid? StatusId { get; set; }

    // Priority — all fields projected from the explicit LEFT JOIN on TicketPriorities
    public Guid? PriorityId { get; set; }
    public int PriorityValue { get; set; }
    public string? PriorityName { get; set; }
    public string? PriorityColor { get; set; }

    // Integration — name projected from the explicit LEFT JOIN on Integrations (null if no integration)
    public string? IntegrationName { get; set; }
    public Guid? IntegrationId { get; set; }
    public string? ExternalTicketId { get; set; }

    // Assignments
    public Guid? AssignedAgentId { get; set; }
    public Guid? AssignedWorkflowId { get; set; }

    public bool IsInternal { get; set; }

    /// <summary>
    /// Scalar comment count resolved by a correlated sub-query (COUNT(*) on TicketComments).
    /// No comment objects are loaded in the list query path.
    /// </summary>
    public int CommentCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
