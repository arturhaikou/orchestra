namespace Orchestra.Application.AiCliIntegrations.DTOs;

public record UpdateAiCliIntegrationRequest(
    Guid WorkspaceId,
    string Name,
    string? Credential,
    bool UseLoggedInUser,
    string WorkingDirectory,
    string? CliPath = null);
