namespace Orchestra.Application.Integrations.DTOs;

public record McpToolDto(
    string Name,
    string? Description,
    string DangerLevel,
    bool Enabled
);
