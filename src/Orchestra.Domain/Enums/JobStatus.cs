namespace Orchestra.Domain.Enums;

public enum JobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    WaitingForInput = 4,
    Cancelled = 5
}
