namespace Orchestra.Application.Integrations.DTOs;

public record CreateIntegrationResponse(
    Guid Id,
    string Name,
    string TransportType,
    string EndpointUrl,
    string AuthType,
    bool IsActive,
    int ToolCount,
    DateTime CreatedAt,
    IReadOnlyList<McpToolDto>? Tools = null);
