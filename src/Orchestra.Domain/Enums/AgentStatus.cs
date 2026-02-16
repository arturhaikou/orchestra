namespace Orchestra.Domain.Enums;

/// <summary>
/// Represents the runtime status of an agent.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// Agent is idle and available for work.
    /// </summary>
    Idle,

    /// <summary>
    /// Agent is currently processing a task.
    /// </summary>
    Busy,

    /// <summary>
    /// Agent is offline and unavailable.
    /// </summary>
    Offline
}