namespace Orchestra.Domain.ValueObjects;

public sealed record McpToolDiscoveryResult(
    IReadOnlyList<DiscoveredMcpTool> Tools
)
{
    public int ToolCount => Tools.Count;
}
