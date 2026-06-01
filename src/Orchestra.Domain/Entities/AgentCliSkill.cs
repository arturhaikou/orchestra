namespace Orchestra.Domain.Entities;

public class AgentCliSkill
{
    public Guid AgentId { get; private set; }

    public string SkillName { get; private set; } = string.Empty;

    private AgentCliSkill() { }

    public static AgentCliSkill Create(Guid agentId, string skillName)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));

        if (string.IsNullOrWhiteSpace(skillName))
            throw new ArgumentException("Skill name cannot be empty.", nameof(skillName));

        if (skillName.Trim().Length > 200)
            throw new ArgumentException("Skill name cannot exceed 200 characters.", nameof(skillName));

        return new AgentCliSkill
        {
            AgentId = agentId,
            SkillName = skillName.Trim()
        };
    }
}
