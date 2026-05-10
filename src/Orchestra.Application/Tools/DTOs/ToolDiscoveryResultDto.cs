namespace Orchestra.Application.Tools.DTOs;

public record ToolDiscoveryResultDto(
    Guid IntegrationId,
    string IntegrationName,
    Guid CategoryId,
    int TotalToolCount,
    int SafeCount,
    int ModerateCount,
    int DestructiveCount,
    IReadOnlyList<DiscoveredToolDto> Tools
);
