namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Request model for creating a new integration.
/// </summary>
public record CreateIntegrationRequest(
    Guid WorkspaceId,
    string Name,
    string Type,  // Will be parsed to IntegrationType enum
    string Provider,
    string Url,
    string? Username,
    string ApiKey,  // Will be encrypted before storage
    string? FilterQuery,
    bool Vectorize,
    string? JiraType = null,  // "Cloud" or "OnPremise", defaults to Cloud if not specified
    bool? Connected = null  // Optional connection status (defaults to true via domain entity)
);