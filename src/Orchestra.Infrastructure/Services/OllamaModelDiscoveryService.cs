using System.Text.Json;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workspaces.DTOs;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Probes an Ollama server's <c>GET /api/tags</c> endpoint and returns a structured
/// discovery result containing the list of installed model identifiers.
/// </summary>
/// <remarks>
/// <b>Security contract:</b> The <paramref name="endpoint"/> value accepted by
/// <see cref="DiscoverModelsAsync"/> is never echoed in any returned
/// <see cref="OllamaDiscoveryResult.ErrorMessage"/> string.
/// All connectivity and protocol failures are absorbed internally — this method
/// always returns a result rather than throwing.
/// </remarks>
public sealed class OllamaModelDiscoveryService : IOllamaModelDiscoveryService
{
    /// <summary>
    /// Maximum time to wait for the Ollama server to respond.
    /// Prevents the request pipeline from blocking indefinitely on an unreachable host.
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaModelDiscoveryService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<OllamaDiscoveryResult> DiscoverModelsAsync(
        string endpoint,
        CancellationToken cancellationToken)
    {
        var tagsUrl = $"{endpoint.TrimEnd('/')}/api/tags";

        try
        {
            // A new HttpClient per call is acceptable here — IHttpClientFactory manages
            // the underlying handler pool so per-call clients avoid header bleed
            // between concurrent requests.
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = DefaultTimeout;

            var response = await httpClient.GetAsync(tagsUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Discard the response body — it may contain diagnostic detail that
                // must not be forwarded. The submitted endpoint URL must NOT appear
                // in the error message.
                return new OllamaDiscoveryResult(
                    IsValid: false,
                    Models: Array.Empty<string>(),
                    ErrorMessage: "The Ollama server returned an error response.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var models = doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(element => element.GetProperty("name").GetString()!)
                .ToList();

            return new OllamaDiscoveryResult(
                IsValid: true,
                Models: models.AsReadOnly(),
                ErrorMessage: null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Sanitised result: the inner exception is retained for diagnostics but
            // its message (which may contain the endpoint URL) must not reach the caller's
            // HTTP response body. The submitted URL is never echoed.
            _ = ex; // retained for potential future structured logging
            return new OllamaDiscoveryResult(
                IsValid: false,
                Models: Array.Empty<string>(),
                ErrorMessage: "The Ollama server could not be reached.");
        }
    }
}
