namespace Orchestra.Application.Workspaces.DTOs;

/// <summary>
/// Request body for <c>POST /v1/provider/ollama/models</c>.
/// The field is nullable so the controller can return a field-specific
/// <c>400 Bad Request</c> message when the value is absent.
/// </summary>
public record DiscoverOllamaModelsRequest
{
    /// <summary>The base URL of the Ollama server (e.g., <c>http://localhost:11434</c>).</summary>
    public string? Endpoint { get; init; }
}
