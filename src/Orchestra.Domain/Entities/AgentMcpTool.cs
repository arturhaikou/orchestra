namespace Orchestra.Domain.Entities;

public sealed class AgentMcpTool
{
    public Guid AgentId { get; private set; }
    public Guid McpServerId { get; private set; }
    public string ToolName { get; private set; } = string.Empty;

    private AgentMcpTool() { }

    public static AgentMcpTool Create(Guid agentId, Guid mcpServerId, string toolName)
    {
        if (agentId == Guid.Empty) throw new ArgumentException("AgentId must not be empty.", nameof(agentId));
        if (mcpServerId == Guid.Empty) throw new ArgumentException("McpServerId must not be empty.", nameof(mcpServerId));
        if (string.IsNullOrWhiteSpace(toolName)) throw new ArgumentException("ToolName must not be empty.", nameof(toolName));

        return new AgentMcpTool
        {
            AgentId = agentId,
            McpServerId = mcpServerId,
            ToolName = toolName.Trim()
        };
    }
}
