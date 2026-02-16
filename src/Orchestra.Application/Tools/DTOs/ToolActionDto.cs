namespace Orchestra.Application.Tools.DTOs;

/// <summary>
/// Response DTO for individual tool action.
/// Represents a specific action that can be performed within a tool category.
/// </summary>
public record ToolActionDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    string? DangerLevel
);