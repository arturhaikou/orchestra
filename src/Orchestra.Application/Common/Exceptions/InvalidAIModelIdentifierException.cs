using System;
using System.Collections.Generic;

namespace Orchestra.Application.Common.Exceptions;

/// <summary>
/// Exception raised when one or more submitted AI model identifiers are not found
/// in the list of available models from the active AI provider.
/// </summary>
public class InvalidAIModelIdentifierException : Exception
{
    /// <summary>
    /// Collection of validation violations, mapping AI feature names to their invalid model identifiers.
    /// Example: { "AI Summarization": "unknown-model-123", "Customer Satisfaction Analysis": "bad-model-456" }
    /// </summary>
    public IReadOnlyDictionary<string, string> InvalidModelsByFeature { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidAIModelIdentifierException"/> class.
    /// </summary>
    /// <param name="invalidModelsByFeature">
    /// A dictionary mapping AI feature names (e.g., "AI Summarization") to the invalid model identifier strings.
    /// Must contain at least one entry.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if invalidModelsByFeature is null or empty.</exception>
    public InvalidAIModelIdentifierException(Dictionary<string, string> invalidModelsByFeature)
        : base(BuildMessage(invalidModelsByFeature))
    {
        if (invalidModelsByFeature == null || invalidModelsByFeature.Count == 0)
        {
            throw new ArgumentException(
                "At least one invalid model identifier must be provided.",
                nameof(invalidModelsByFeature));
        }

        InvalidModelsByFeature = invalidModelsByFeature.AsReadOnly();
    }

    private static string BuildMessage(Dictionary<string, string> invalidModelsByFeature)
    {
        if (invalidModelsByFeature == null || invalidModelsByFeature.Count == 0)
        {
            return "One or more AI model identifiers are invalid.";
        }

        var violations = string.Join(
            "; ",
            invalidModelsByFeature.Select(kvp => $"The model '{kvp.Value}' specified for {kvp.Key} is not available."));

        return violations;
    }
}
