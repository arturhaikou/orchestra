namespace Orchestra.Application.Common.Configuration;

/// <summary>
/// Configuration settings for automated agent execution.
/// </summary>
public class AgentExecutionSettings
{
    public const string SectionName = "AgentExecution";

    /// <summary>
    /// AI provider identifier. Valid values: "Azure" (default), "Ollama".
    /// Injected by the AppHost via the AgentExecution__Provider environment variable.
    /// </summary>
    public string Provider { get; set; } = "Azure";

    /// <summary>
    /// AI model deployment name (Azure OpenAI) or model tag (Ollama) used for agent execution.
    /// </summary>
    public string ModelDeploymentName { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Polling interval in seconds for checking tickets eligible for agent execution.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;
}
