namespace Orchestra.Application.Workspaces.DTOs;

/// <summary>
/// Request body for <c>PUT /v1/workspaces/{id}/provider</c>.
/// Carries the new provider type, provider-specific credentials, and the desired default model.
/// </summary>
/// <param name="ProviderType">
/// The AI provider to configure. Accepted values: <c>"AzureOpenAI"</c>, <c>"Ollama"</c>.
/// </param>
/// <param name="Endpoint">
/// Azure OpenAI endpoint URL in plaintext. Required when <paramref name="ProviderType"/> is
/// <c>"AzureOpenAI"</c>; must be absent or null for <c>"Ollama"</c>.
/// </param>
/// <param name="ApiKey">
/// Azure OpenAI API key in plaintext. Required when <paramref name="ProviderType"/> is
/// <c>"AzureOpenAI"</c>; must be absent or null for <c>"Ollama"</c>.
/// </param>
/// <param name="DefaultModelId">
/// The model identifier to set as the workspace default. Must be present in the live
/// provider's model list confirmed during the validation probe.
/// </param>
public sealed record ReconfigureProviderRequest(
    string? ProviderType,
    string? Endpoint,
    string? ApiKey,
    string? DefaultModelId);
