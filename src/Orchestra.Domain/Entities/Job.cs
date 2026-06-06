using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

public class Job
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid AgentId { get; private set; }
    public string AgentName { get; private set; } = string.Empty;
    public JobStatus Status { get; private set; }
    public JobTriggerType TriggerType { get; private set; }
    public Guid? TicketId { get; private set; }
    public string? TicketTitle { get; private set; }
    public string InitialPrompt { get; private set; } = string.Empty;
    public string? FinalResponse { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? ParentJobId { get; private set; }
    public Guid? WorkflowExecutionId { get; private set; }

    private Job() { }

    public static Job Create(
        Guid workspaceId,
        Guid agentId,
        string agentName,
        JobTriggerType triggerType,
        string initialPrompt,
        Guid? ticketId = null,
        string? ticketTitle = null)
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            AgentId = agentId,
            AgentName = agentName,
            TriggerType = triggerType,
            InitialPrompt = initialPrompt,
            TicketId = ticketId,
            TicketTitle = ticketTitle,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRunning()
    {
        Status = JobStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void MarkCompleted(string? finalResponse)
    {
        Status = JobStatus.Completed;
        FinalResponse = finalResponse;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkWaitingForInput()
    {
        Status = JobStatus.WaitingForInput;
    }

    public void MarkCancelled()
    {
        Status = JobStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    public void SetParent(Guid parentJobId)
    {
        ParentJobId = parentJobId;
    }

    public void AssignWorkflowExecution(Guid workflowExecutionId)
    {
        WorkflowExecutionId = workflowExecutionId;
    }

    public static Job CreateWorkflowJob(
        Guid workspaceId,
        string workflowName,
        Guid workflowExecutionId,
        Guid? ticketId = null,
        string? ticketTitle = null)
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            AgentId = Guid.Empty,
            AgentName = workflowName,
            TriggerType = JobTriggerType.Ticket,
            InitialPrompt = string.Empty,
            TicketId = ticketId,
            TicketTitle = ticketTitle,
            WorkflowExecutionId = workflowExecutionId,
            Status = JobStatus.Running,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow
        };
    }
}
