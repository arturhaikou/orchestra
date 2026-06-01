using Orchestra.Application.Skills.DTOs;

namespace Orchestra.Application.Agents.DTOs;

/// <summary>
/// Response DTO for Agent entity.
/// Exactly one of <see cref="CustomInstructions"/> or <see cref="ProjectPrinciples"/> is
/// non-null for a given agent, depending on its tool configuration.
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
    string[] McpServerNames,    // MCP server names assigned to this agent
    string[] SubAgentIds,       // IDs of agents assigned as sub-agents of this agent
    string AvatarUrl,
    string? CustomInstructions,
    string? ProjectPrinciples,
    string? Model,
    string? TemplateId,
    int? TemplateVersion,
    bool IsBuiltIn,
    string? Guide = null,
    string? AiCliIntegrationId = null,
    string? ReasoningEffort = null,
    IReadOnlyList<SkillDto>? Skills = null,
    string[]? SkillFolderIds = null,
    string[]? CliSkillNames = null
);