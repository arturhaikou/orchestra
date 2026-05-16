namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents the many-to-many relationship between agents and skills.
/// This join table allows granular assignment of skills to agents.
/// </summary>
public class AgentSkill
{
    /// <summary>
    /// Gets the agent identifier (part of composite primary key).
    /// </summary>
    public Guid AgentId { get; private set; }

    /// <summary>
    /// Gets the skill identifier (part of composite primary key).
    /// </summary>
    public Guid SkillId { get; private set; }

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private AgentSkill() { }

    /// <summary>
    /// Creates a new <see cref="AgentSkill"/> instance with validation.
    /// </summary>
    /// <param name="agentId">The ID of the agent.</param>
    /// <param name="skillId">The ID of the skill.</param>
    /// <returns>A new <see cref="AgentSkill"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static AgentSkill Create(Guid agentId, Guid skillId)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));

        if (skillId == Guid.Empty)
            throw new ArgumentException("Skill ID cannot be empty.", nameof(skillId));

        return new AgentSkill
        {
            AgentId = agentId,
            SkillId = skillId
        };
    }
}
