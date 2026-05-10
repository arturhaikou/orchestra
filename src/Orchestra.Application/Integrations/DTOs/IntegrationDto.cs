namespace Orchestra.Application.Integrations.DTOs;

public record IntegrationDto(
    string Id,
    string WorkspaceId,
    string Name,
    string[] Types,
    string? Icon,
    string? Provider,
    string? Url,
    string? Username,
    bool Connected,
    string? LastSync,
    string? FilterQuery,
    bool Vectorize,
    string? JiraType = null,
    string? ConfluenceType = null,
    bool IsMcpBacked = false,
    string? McpEndpointUrl = null,
    int? ToolCount = null,
    string? McpTransportType = null,
    string? McpCommand = null
);
