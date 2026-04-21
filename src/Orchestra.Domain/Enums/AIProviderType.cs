namespace Orchestra.Domain.Enums;

/// <summary>
/// Identifies the AI provider used for a workspace's AI configuration.
/// </summary>
public enum AIProviderType
{
    /// <summary>
    /// Microsoft Azure OpenAI Service.
    /// Requires <c>Endpoint</c> and <c>ApiKey</c> to be set on the configuration.
    /// </summary>
    AzureOpenAI,

    /// <summary>
    /// Ollama self-hosted model server.
    /// Requires <c>OllamaBaseUrl</c> to be set on the configuration.
    /// </summary>
    Ollama
}
