namespace Orchestra.Application.Integrations.DTOs;

public record McpStdioDiscoveryRequest(
    string Command,
    string[]? Arguments,
    Dictionary<string, string>? PlaintextEnvironmentVariables,
    string IntegrationName
);
