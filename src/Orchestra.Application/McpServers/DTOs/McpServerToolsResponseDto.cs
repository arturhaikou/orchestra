namespace Orchestra.Application.McpServers.DTOs;

public record McpServerToolsResponseDto(
    bool IsSuccess,
    IReadOnlyList<McpToolItemDto>? Tools,
    string? ErrorType,
    string? ErrorMessage
);

public record McpToolItemDto(
    string Name,
    string? Description,
    string DangerLevel
);
