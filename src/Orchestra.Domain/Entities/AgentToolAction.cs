namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents the many-to-many relationship between agents and tool actions.
/// This join table allows granular assignment of tool capabilities to agents.
/// </summary>
public class AgentToolAction
{
    /// <summary>
    /// Gets the agent identifier (part of composite primary key).
    /// </summary>
    public Guid AgentId { get; private set; }

    /// <summary>
    /// Gets the tool action identifier (part of composite primary key).
    /// </summary>
    public Guid ToolActionId { get; private set; }

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private AgentToolAction() { } // EF Core constructor

    /// <summary>
    /// Creates a new AgentToolAction instance with validation.
    /// </summary>
    /// <param name="agentId">The ID of the agent.</param>
    /// <param name="toolActionId">The ID of the tool action.</param>
    /// <returns>A new AgentToolAction instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static AgentToolAction Create(Guid agentId, Guid toolActionId)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));

        if (toolActionId == Guid.Empty)
            throw new ArgumentException("Tool action ID cannot be empty.", nameof(toolActionId));

        return new AgentToolAction
        {
            AgentId = agentId,
            ToolActionId = toolActionId
        };
    }
}