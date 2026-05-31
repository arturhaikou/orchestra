using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

public class WorkflowStepExecution
{
    public Guid Id { get; private set; }
    public Guid WorkflowExecutionId { get; private set; }
    public int StepIndex { get; private set; }
    public Guid? JobId { get; private set; }
    public WorkflowExecutionStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Output { get; private set; }

    private WorkflowStepExecution() { }

    public static WorkflowStepExecution Create(Guid workflowExecutionId, int stepIndex)
    {
        return new WorkflowStepExecution
        {
            Id = Guid.NewGuid(),
            WorkflowExecutionId = workflowExecutionId,
            StepIndex = stepIndex,
            Status = WorkflowExecutionStatus.Running,
            StartedAt = DateTime.UtcNow
        };
    }

    public void AssignJob(Guid jobId)
    {
        JobId = jobId;
    }

    public void MarkCompleted(string? output)
    {
        Status = WorkflowExecutionStatus.Completed;
        Output = output;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = WorkflowExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkWaitingForInput()
    {
        Status = WorkflowExecutionStatus.WaitingForInput;
    }
}
