namespace Orchestra.Application.Tools.DTOs;

public record ToolActionDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    string? DangerLevel,
    bool IsEnabled,
    bool IsMcpTool,
    string? McpToolSchema,
    string Source = "native",
    Guid? IntegrationId = null,
    string? Transport = null,
    string? IntegrationName = null
);