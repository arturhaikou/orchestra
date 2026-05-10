namespace Orchestra.Application.Integrations.DTOs;

public record SyncToolsResultDto(
    int Added,
    int Removed,
    int Updated,
    int Total,
    List<SyncedToolSummaryDto> Tools);
