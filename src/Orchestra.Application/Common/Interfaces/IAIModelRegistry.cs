namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// In-memory registry of AI models available from the configured provider.
/// Populated once at application startup using <see cref="IAIModelListService"/>.
/// Enables efficient, non-blocking availability checks during per-request model resolution.
/// </summary>
public interface IAIModelRegistry
{
    /// <summary>
    /// Checks whether a model identifier is currently available from the provider.
    /// Returns immediately without making any I/O calls (data is pre-loaded at startup).
    /// </summary>
    /// <param name="modelId">The model identifier to check. If null, returns false.</param>
    /// <returns>True if the model is available, false otherwise (including for null input).</returns>
    bool IsModelAvailable(string? modelId);
}
