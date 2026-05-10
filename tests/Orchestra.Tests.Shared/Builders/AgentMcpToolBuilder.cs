using Orchestra.Domain.Entities;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating <see cref="AgentMcpTool"/> test instances.
/// Defaults to a randomly generated agent, server, and tool name.
/// </summary>
public sealed class AgentMcpToolBuilder
{
    private Guid _agentId = Guid.NewGuid();
    private Guid _mcpServerId = Guid.NewGuid();
    private string _toolName = $"tool_{Guid.NewGuid():N}";

    /// <summary>Sets the agent ID.</summary>
    public AgentMcpToolBuilder WithAgentId(Guid agentId)
    {
        _agentId = agentId;
        return this;
    }

    /// <summary>Sets the MCP server ID.</summary>
    public AgentMcpToolBuilder WithMcpServerId(Guid mcpServerId)
    {
        _mcpServerId = mcpServerId;
        return this;
    }

    /// <summary>Sets the tool name.</summary>
    public AgentMcpToolBuilder WithToolName(string toolName)
    {
        _toolName = toolName;
        return this;
    }

    /// <summary>Builds the <see cref="AgentMcpTool"/> instance.</summary>
    public AgentMcpTool Build() =>
        AgentMcpTool.Create(_agentId, _mcpServerId, _toolName);
}
