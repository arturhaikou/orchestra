using System.ComponentModel.DataAnnotations;

namespace Orchestra.Application.Integrations.DTOs;

public record CreateHttpMcpIntegrationRequest(
    [Required][MinLength(2)][MaxLength(100)] string Name,
    [Required] string TransportType,
    [Required][MaxLength(500)] string EndpointUrl,
    [Required] string AuthType,
    [MaxLength(4096)] string? ApiKey,
    string? McpCommand,
    string? McpArguments,
    string? McpEnvironmentVariables
);
