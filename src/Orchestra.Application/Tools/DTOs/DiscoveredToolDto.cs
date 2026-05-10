namespace Orchestra.Application.Tools.DTOs;

public record DiscoveredToolDto(
    Guid Id,
    string Name,
    string? Description,
    string DangerLevel,
    bool IsEnabled,
    string? McpToolSchema
);
