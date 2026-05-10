using System.ComponentModel.DataAnnotations;

namespace Orchestra.Application.Integrations.DTOs;

public record CreateStdioMcpIntegrationRequest(
    [Required][MinLength(2)][MaxLength(100)] string Name,
    [Required][MaxLength(500)] string Command,
    string[]? Arguments,
    Dictionary<string, string>? EnvironmentVariables,
    string? EndpointUrl,
    string? AuthType
);
