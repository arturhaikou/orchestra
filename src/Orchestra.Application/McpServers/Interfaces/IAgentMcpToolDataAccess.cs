using Orchestra.Domain.Entities;

namespace Orchestra.Application.McpServers.Interfaces;

public interface IAgentMcpToolDataAccess
{
    Task<int> CountDistinctAgentsByServerIdAsync(
        Guid mcpServerId,
        CancellationToken cancellationToken = default);

    Task<List<AgentMcpTool>> GetByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task<List<AgentMcpTool>> GetByAgentAndServerIdAsync(
        Guid agentId,
        Guid mcpServerId,
        CancellationToken cancellationToken = default);

    Task<string[]> GetMcpServerNamesByAgentIdAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);

    Task ReplaceForAgentAndServerAsync(
        Guid agentId,
        Guid mcpServerId,
        IReadOnlyList<AgentMcpTool> replacements,
        CancellationToken cancellationToken = default);

    Task DeleteAllForAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken = default);
}
