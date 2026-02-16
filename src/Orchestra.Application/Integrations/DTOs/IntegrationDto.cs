namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Data transfer object for integration API responses.
/// API keys are intentionally excluded for security.
/// </summary>
public record IntegrationDto(
    string Id,
    string WorkspaceId,
    string Name,
    string Type,
    string? Icon,
    string? Provider,
    string? Url,
    string? Username,
    bool Connected,
    string? LastSync,  // Formatted as human-readable (e.g., "2 hours ago")
    string? FilterQuery,
    bool Vectorize
);
