namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents the many-to-many relationship between a parent agent and its sub-agents.
/// A sub-agent is an existing agent that is assigned as a callable tool inside another agent.
/// </summary>
public class AgentSubAgent
{
    /// <summary>
    /// Gets the parent agent identifier (part of composite primary key).
    /// </summary>
    public Guid ParentAgentId { get; private set; }

    /// <summary>
    /// Gets the sub-agent identifier (part of composite primary key).
    /// </summary>
    public Guid SubAgentId { get; private set; }

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private AgentSubAgent() { } // EF Core constructor

    /// <summary>
    /// Creates a new AgentSubAgent assignment with validation.
    /// </summary>
    /// <param name="parentAgentId">The ID of the parent agent.</param>
    /// <param name="subAgentId">The ID of the sub-agent.</param>
    /// <returns>A new AgentSubAgent instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static AgentSubAgent Create(Guid parentAgentId, Guid subAgentId)
    {
        if (parentAgentId == Guid.Empty)
            throw new ArgumentException("Parent agent ID cannot be empty.", nameof(parentAgentId));

        if (subAgentId == Guid.Empty)
            throw new ArgumentException("Sub-agent ID cannot be empty.", nameof(subAgentId));

        if (parentAgentId == subAgentId)
            throw new ArgumentException("An agent cannot be assigned as its own sub-agent.", nameof(subAgentId));

        return new AgentSubAgent
        {
            ParentAgentId = parentAgentId,
            SubAgentId = subAgentId
        };
    }
}
