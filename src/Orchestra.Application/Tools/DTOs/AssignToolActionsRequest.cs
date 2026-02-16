namespace Orchestra.Application.Tools.DTOs;

/// <summary>
/// Request DTO for assigning tool actions to an agent.
/// ToolActionIds must be non-empty (validation handled in application handler).
/// </summary>
public record AssignToolActionsRequest(
    List<Guid> ToolActionIds
);