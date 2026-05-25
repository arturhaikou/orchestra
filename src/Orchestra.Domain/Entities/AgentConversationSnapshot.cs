namespace Orchestra.Domain.Entities;

public class AgentConversationSnapshot
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public Guid AgentId { get; private set; }
    public string SerializedSessionJson { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AgentConversationSnapshot() { }

    public static AgentConversationSnapshot Create(
        Guid jobId,
        Guid agentId,
        string serializedSessionJson)
    {
        var now = DateTime.UtcNow;
        return new AgentConversationSnapshot
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            AgentId = agentId,
            SerializedSessionJson = serializedSessionJson,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string serializedSessionJson)
    {
        SerializedSessionJson = serializedSessionJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
