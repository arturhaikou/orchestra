namespace Orchestra.Application.AiCliIntegrations.DTOs;

/// <summary>
/// Represents metadata for an AI model, including reasoning effort capabilities.
/// Flattened representation of GitHub Copilot SDK's ModelInfo.
/// </summary>
public sealed class ModelMetadataDto
{
    /// <summary>
    /// The unique identifier for the model (e.g., "o1", "gpt-4-turbo").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// List of supported reasoning effort levels for this model.
    /// Null or empty if the model does not support reasoning effort.
    /// </summary>
    public IList<string>? SupportedReasoningEfforts { get; set; }

    /// <summary>
    /// The default reasoning effort level for this model.
    /// Null if the model does not support reasoning effort.
    /// </summary>
    public string? DefaultReasoningEffort { get; set; }
}
