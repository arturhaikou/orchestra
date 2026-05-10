namespace Orchestra.Application.Integrations.DTOs;

public record McpIntegrationDto(
    string Id,
    string WorkspaceId,
    string Name,
    string Provider,
    string McpEndpointUrl,
    string McpAuthType,
    bool IsMcpBacked,
    int ToolCount,
    IReadOnlyList<McpToolDto> DiscoveredTools
);
