namespace Orchestra.Application.Workspaces.DTOs;

/// <summary>
/// Structured result returned by <c>IOllamaModelDiscoveryService.DiscoverModelsAsync</c>
/// and serialised by the API as the <c>POST /v1/provider/ollama/models</c> response body.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> when the Ollama server is reachable and returns a valid model
/// listing (including an empty list); <see langword="false"/> when the connectivity probe failed.
/// </param>
/// <param name="Models">
/// Model identifier strings when <paramref name="IsValid"/> is <see langword="true"/>;
/// empty when <see langword="false"/>.
/// </param>
/// <param name="ErrorMessage">
/// Human-readable description of the failure when <paramref name="IsValid"/> is
/// <see langword="false"/>; <see langword="null"/> when connectivity succeeded.
/// Must never contain the submitted endpoint URL, raw stack traces, or internal service URLs.
/// </param>
public sealed record OllamaDiscoveryResult(
    bool IsValid,
    IReadOnlyList<string> Models,
    string? ErrorMessage);
