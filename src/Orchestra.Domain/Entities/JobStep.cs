using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

public class JobStep
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public JobStepType StepType { get; private set; }
    public int Sequence { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? Content { get; private set; }
    public bool IsJson { get; private set; }
    public string? ToolName { get; private set; }
    public long? DurationMs { get; private set; }
    public bool IsError { get; private set; }
    public Guid? ParentStepId { get; private set; }
    public Guid? AgentId { get; private set; }
    public string? AgentName { get; private set; }

    private JobStep() { }

    public static JobStep Create(
        Guid jobId,
        JobStepType stepType,
        int sequence,
        string? content = null,
        bool isJson = false,
        string? toolName = null,
        long? durationMs = null,
        bool isError = false,
        Guid? parentStepId = null,
        Guid? agentId = null,
        string? agentName = null)
    {
        return new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            StepType = stepType,
            Sequence = sequence,
            Timestamp = DateTime.UtcNow,
            Content = content,
            IsJson = isJson,
            ToolName = toolName,
            DurationMs = durationMs,
            IsError = isError,
            ParentStepId = parentStepId,
            AgentId = agentId,
            AgentName = agentName
        };
    }
}
