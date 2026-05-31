namespace Orchestra.Domain.Enums;

public enum WorkflowExecutionStatus
{
    Pending = 0,
    Running = 1,
    WaitingForInput = 2,
    Completed = 3,
    Failed = 4
}
