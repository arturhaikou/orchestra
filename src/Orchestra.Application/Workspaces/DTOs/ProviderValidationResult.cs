namespace Orchestra.Application.Workspaces.DTOs;

/// <summary>
/// Structured result returned by <c>IWorkspaceProviderService.ValidateProviderAsync</c>
/// and serialised by the API as the <c>POST /v1/workspaces/{id}/provider/validate</c>
/// response body.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> when the provider is reachable and credentials are accepted;
/// <see langword="false"/> when the connectivity probe failed.
/// </param>
/// <param name="ProviderType">
/// The string representation of the workspace's configured <c>AIProviderType</c>
/// (e.g., <c>"AzureOpenAI"</c> or <c>"Ollama"</c>).
/// </param>
/// <param name="Models">
/// Live list of model deployment names when <paramref name="IsValid"/> is
/// <see langword="true"/>; empty when <see langword="false"/>.
/// </param>
/// <param name="ErrorMessage">
/// Human-readable description of the failure when <paramref name="IsValid"/> is
/// <see langword="false"/>; <see langword="null"/> when connectivity succeeded.
/// Must never contain credential values, raw stack traces, or internal service URLs.
/// </param>
/// <param name="OllamaBaseUrl">
/// The stored Ollama server base URL when <paramref name="ProviderType"/> is
/// <c>"Ollama"</c>; <see langword="null"/> for all other provider types.
/// This value is stored as plaintext and is safe to surface to authenticated
/// workspace members. It must never be logged.
/// </param>
public sealed record ProviderValidationResult(
    bool IsValid,
    string ProviderType,
    IReadOnlyList<string> Models,
    string? ErrorMessage,
    string? OllamaBaseUrl = null);
