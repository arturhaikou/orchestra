using Orchestra.Domain.Enums;

namespace Orchestra.Application.Integrations.DTOs;

public record SeededToolSummary(
    Guid ToolActionId,
    string McpToolName,
    DangerLevel DangerLevel
);
