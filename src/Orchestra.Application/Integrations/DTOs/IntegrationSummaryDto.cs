using Orchestra.Domain.Enums;

namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Lightweight, credential-free projection of an integration for use in agent system prompt injection.
/// Contains only non-sensitive metadata — ID, display name, and provider type.
/// Credential fields (EncryptedApiKey, Username) are intentionally excluded.
/// </summary>
public record IntegrationSummaryDto(
    Guid Id,
    string Name,
    ProviderType Provider);
