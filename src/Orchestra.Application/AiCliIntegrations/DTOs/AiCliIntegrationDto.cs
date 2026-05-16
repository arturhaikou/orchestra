using Orchestra.Domain.Enums;

namespace Orchestra.Application.AiCliIntegrations.DTOs;

public record AiCliIntegrationDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    AiCliProviderType Provider,
    bool UseLoggedInUser,
    string WorkingDirectory,
    string? CliPath,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
