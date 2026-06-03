using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

public class WorkflowExecution
{
    public Guid Id { get; private set; }
    public Guid WorkflowDefinitionId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public WorkflowExecutionStatus Status { get; private set; }
    public int CurrentStepIndex { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? WorkflowJobId { get; private set; }
    public Guid? ActiveTicketId { get; private set; }

    private WorkflowExecution() { }

    public static WorkflowExecution Create(
        Guid workflowDefinitionId,
        Guid ticketId,
        Guid workspaceId)
    {
        return new WorkflowExecution
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = workflowDefinitionId,
            TicketId = ticketId,
            WorkspaceId = workspaceId,
            Status = WorkflowExecutionStatus.Running,
            CurrentStepIndex = 0,
            StartedAt = DateTime.UtcNow
        };
    }

    public void AdvanceToStep(int stepIndex)
    {
        CurrentStepIndex = stepIndex;
        Status = WorkflowExecutionStatus.Running;
    }

    public void MarkWaitingForInput()
    {
        Status = WorkflowExecutionStatus.WaitingForInput;
    }

    public void MarkCompleted()
    {
        Status = WorkflowExecutionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = WorkflowExecutionStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }

    public void AssignWorkflowJob(Guid jobId)
    {
        WorkflowJobId = jobId;
    }

    public void SwitchActiveTicket(Guid newTicketId)
    {
        ActiveTicketId = newTicketId;
    }
}
