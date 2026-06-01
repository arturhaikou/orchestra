namespace Orchestra.Domain.Entities;

public class AgentSkillFolder
{
    public Guid AgentId { get; private set; }

    public Guid SkillFolderId { get; private set; }

    private AgentSkillFolder() { }

    public static AgentSkillFolder Create(Guid agentId, Guid skillFolderId)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));

        if (skillFolderId == Guid.Empty)
            throw new ArgumentException("Skill folder ID cannot be empty.", nameof(skillFolderId));

        return new AgentSkillFolder
        {
            AgentId = agentId,
            SkillFolderId = skillFolderId
        };
    }
}
