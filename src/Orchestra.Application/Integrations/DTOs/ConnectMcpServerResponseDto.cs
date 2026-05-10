namespace Orchestra.Application.Integrations.DTOs;

public sealed record ConnectMcpServerResponseDto(
    IReadOnlyList<ToolPreviewDto> Tools
);
