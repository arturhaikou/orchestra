using Orchestra.Application.McpServers.Interfaces;

namespace Orchestra.Application.McpServers;

public sealed class McpServerImpactCounter : IMcpServerImpactCounter
{
    private readonly IAgentMcpToolDataAccess _agentMcpToolDataAccess;

    public McpServerImpactCounter(IAgentMcpToolDataAccess agentMcpToolDataAccess)
    {
        _agentMcpToolDataAccess = agentMcpToolDataAccess;
    }

    public async Task<int> CountImpactedAgentsAsync(
        Guid mcpServerId,
        CancellationToken cancellationToken = default)
    {
        return await _agentMcpToolDataAccess.CountDistinctAgentsByServerIdAsync(
            mcpServerId, cancellationToken);
    }
}
