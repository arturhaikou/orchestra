namespace Orchestra.Application.Common.Configuration;

/// <summary>
/// Configuration settings for automated agent execution.
/// </summary>
public class AgentExecutionSettings
{
    public const string SectionName = "AgentExecution";

    /// <summary>
    /// Azure OpenAI model deployment name used for agent execution.
    /// </summary>
    public string ModelDeploymentName { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Polling interval in seconds for checking tickets eligible for agent execution.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;
}
