namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Request DTO for creating a new Agent.
/// Exactly one of <see cref="CustomInstructions"/> or <see cref="ProjectPrinciples"/> must be
/// non-null. When a review tool action is assigned, <see cref="ProjectPrinciples"/> is expected
/// and <see cref="CustomInstructions"/> must be absent. Mutual exclusivity is enforced by
/// AgentService.
/// </summary>
public record CreateAgentRequest(
    Guid WorkspaceId,
    string Name,
    string Role,
    string[] Capabilities,
    string[]? ToolActionIds,
    string? CustomInstructions,
    string? ProjectPrinciples,
    string? Model,
    IReadOnlyList<McpToolSelectionDto>? McpSelections = null
);