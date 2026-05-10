namespace Orchestra.Application.Integrations.DTOs;

public record McpHttpDiscoveryRequest(
    string EndpointUrl,
    string AuthType,
    string? PlaintextApiKey
);
