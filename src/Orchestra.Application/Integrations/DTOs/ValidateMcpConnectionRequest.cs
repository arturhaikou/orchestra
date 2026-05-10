namespace Orchestra.Application.Integrations.DTOs;

public record ValidateMcpConnectionRequest(
    Guid WorkspaceId,
    string McpEndpointUrl,
    string McpAuthType,
    string? ApiKey
);
