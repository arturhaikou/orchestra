namespace Orchestra.Domain.Entities;

public class AgentQuestion
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AgentId { get; private set; }

    /// <summary>
    /// JSON array of QuestionItem objects.
    /// </summary>
    public string QuestionsJson { get; private set; } = string.Empty;

    /// <summary>JSON object keyed by question index: { "0": "answer1", "1": "answer2" }</summary>
    public string? AnswersJson { get; private set; }

    public Orchestra.Domain.Enums.QuestionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? AnsweredAt { get; private set; }

    private AgentQuestion() { }

    public static AgentQuestion Create(
        Guid jobId,
        Guid workspaceId,
        Guid agentId,
        string questionsJson)
    {
        return new AgentQuestion
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            WorkspaceId = workspaceId,
            AgentId = agentId,
            QuestionsJson = questionsJson,
            Status = Orchestra.Domain.Enums.QuestionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordAnswer(string answersJson)
    {
        AnswersJson = answersJson;
        Status = Orchestra.Domain.Enums.QuestionStatus.Answered;
        AnsweredAt = DateTime.UtcNow;
    }

    public void MarkCancelled()
    {
        Status = Orchestra.Domain.Enums.QuestionStatus.Cancelled;
        AnsweredAt = DateTime.UtcNow;
    }
}
