using Orchestra.Domain.Enums;

namespace Orchestra.Application.McpServers.DTOs;

public record McpToolItem(
    string Name,
    string? Description,
    DangerLevel DangerLevel);
