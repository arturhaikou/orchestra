using System.Collections.Generic;

namespace Orchestra.Application.Tickets.DTOs;

/// <summary>
/// Unified ticket DTO supporting both internal and external tickets.
/// Uses nullable nested objects for status and priority.
/// </summary>
public record TicketDto(
    string Id,                      // Internal: GUID string, External: composite format "{integrationId}:{externalId}"
    Guid WorkspaceId,
    string Title,
    string Description,
    
    // Status and Priority as nested objects
    TicketStatusDto? Status,
    TicketPriorityDto? Priority,
    
    // Common fields
    bool Internal,
    Guid? IntegrationId,            // Null for internal, populated for external
    string? ExternalTicketId,       // Null for internal, e.g., "PROJ-123" for external
    string? ExternalUrl,            // Null for internal, provider URL for external
    string Source,                  // "INTERNAL", "JIRA", "AZURE-DEVOPS", etc.
    
    // Assignments (for both types)
    Guid? AssignedAgentId,
    Guid? AssignedWorkflowId,
    
    // Comments
    List<CommentDto> Comments,
    
    // Additional fields
    int? Satisfaction,              // CSAT score (0-100)
    string? Summary                // AI-generated summary
);

public record TicketStatusDto(Guid Id, string Name, string Color);

public record TicketPriorityDto(Guid Id, string Name, string Color, int Value);