namespace Orchestra.Application.Common.Configuration;

/// <summary>
/// Configuration settings for automated agent execution.
/// </summary>
public class AgentExecutionSettings
{
    public const string SectionName = "AgentExecution";

    /// <summary>
    /// Polling interval in seconds for checking tickets eligible for agent execution.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Seconds to wait for in-flight agent tasks during graceful shutdown.
    /// </summary>
    public int GracefulShutdownTimeoutSeconds { get; set; } = 300;
}
