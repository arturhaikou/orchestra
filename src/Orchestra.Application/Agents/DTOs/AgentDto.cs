namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Response DTO for Agent entity.
/// </summary>
public record AgentDto(
    string Id,
    string WorkspaceId,
    string Name,
    string Role,
    string Status,              // "IDLE", "BUSY", or "OFFLINE"
    string[] Capabilities,
    string[] ToolActionIds,     // Used for create/update operations
    string[] ToolCategories,    // Unique category names for display
    string AvatarUrl,
    string? CustomInstructions
);