namespace Orchestra.Application.Integrations.DTOs;

public record CreateMcpIntegrationRequest(
    Guid WorkspaceId,
    string Name,
    string Provider,
    string McpEndpointUrl,
    string McpAuthType,
    string? ApiKey,
    IReadOnlyList<ToolEnablementOverride>? ToolOverrides
);
