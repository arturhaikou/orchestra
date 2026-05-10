namespace Orchestra.Application.McpServers.Interfaces;

public interface IMcpServerImpactCounter
{
    Task<int> CountImpactedAgentsAsync(
        Guid mcpServerId,
        CancellationToken cancellationToken = default);
}
