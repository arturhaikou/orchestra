using Orchestra.Domain.Enums;

namespace Orchestra.Domain.ValueObjects;

public sealed record DiscoveredMcpTool(
    string Name,
    string? Description,
    DangerLevel DangerLevel,
    string? InputSchemaJson,
    bool Enabled
);
