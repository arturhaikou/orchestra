namespace Orchestra.Application.Integrations.DTOs;

public record McpToolDiscoveryResultDto(
    int ToolCount,
    IReadOnlyList<McpToolDto> Tools
);
