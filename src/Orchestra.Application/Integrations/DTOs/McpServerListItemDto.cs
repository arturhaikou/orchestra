namespace Orchestra.Application.Integrations.DTOs;

/// <summary>
/// Read-optimised projection for the MCP Servers list page.
/// Contains only the fields required for card rendering.
/// </summary>
public record McpServerListItemDto(
    string Id,
    string WorkspaceId,
    string Name,
    string ConnectionStatus,
    string TransportType,
    string? EndpointUrl,
    string? Command,
    string CreatedAt);
