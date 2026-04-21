namespace Orchestra.Application.Workspaces.DTOs;

/// <summary>
/// Request body for <c>POST /v1/provider/azure/models</c>.
/// Both fields are nullable so the controller can return field-specific
/// <c>400 Bad Request</c> messages when either value is absent.
/// </summary>
/// <remarks>
/// <b>Security contract:</b> The <see cref="ApiKey"/> property must never be
/// echoed in a response body, log entry, or distributed trace.
/// </remarks>
public record DiscoverAzureModelsRequest
{
    /// <summary>The Azure OpenAI endpoint URL (e.g., <c>https://my-resource.openai.azure.com</c>).</summary>
    public string? Endpoint { get; init; }

    /// <summary>The Azure OpenAI API key. Treated as a secret — never persisted or logged.</summary>
    public string? ApiKey { get; init; }
}
