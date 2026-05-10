namespace Orchestra.Application.Integrations.DTOs;

public record CreateStdioIntegrationResponse(
    Guid Id,
    string Name,
    string TransportType,
    string Command,
    string[]? Arguments,
    bool IsActive,
    int ToolCount,
    IReadOnlyList<SeededToolSummary> Tools,
    DateTime CreatedAt
);
