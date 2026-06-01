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
    /// <summary>Custom instructions for the agent (unlimited length; directly persisted to database text column).</summary>
    string? CustomInstructions,
    /// <summary>Project principles for code review agents (unlimited length; directly persisted to database text column).</summary>
    string? ProjectPrinciples,
    string? Model,
    string? ReasoningEffort = null,
    IReadOnlyList<McpToolSelectionDto>? McpSelections = null,
    string[]? SubAgentIds = null,
    string[]? SkillIds = null,
    string[]? SkillFolderIds = null
);