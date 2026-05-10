namespace Orchestra.Application.Tools.DTOs;

public record ToolActionDetailDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    string DangerLevel,
    bool IsEnabled,
    bool IsMcpTool,
    string? McpToolSchema
);
