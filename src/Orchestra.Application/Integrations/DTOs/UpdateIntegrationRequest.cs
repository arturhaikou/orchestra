namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Request model for updating an existing integration.
/// </summary>
public record UpdateIntegrationRequest(
    string Name,
    string[] Types,  // Each element will be parsed to IntegrationType enum; must be non-empty
    string? Provider,
    string? Url,
    string? Username,
    string? ApiKey,  // If contains "••••••••••••", preserve existing encrypted key
    string? FilterQuery,
    bool Vectorize,
    bool? Connected = null  // Optional connection status (only updated if provided)
);