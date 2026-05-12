using Orchestra.Domain.Enums;

namespace Orchestra.Application.AiCliIntegrations.DTOs;

public record CreateAiCliIntegrationRequest(
    Guid WorkspaceId,
    string Name,
    AiCliProviderType Provider,
    string? Credential,
    bool UseLoggedInUser,
    string WorkingDirectory,
    string? ModelId = null,
    string? CliPath = null);
