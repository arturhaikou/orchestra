using System.Text.Json;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Queries an Azure OpenAI resource for the names of its available model deployments
/// by calling the data-plane <c>GET /openai/models</c> endpoint.
/// </summary>
/// <remarks>
/// <b>Security contract:</b> The <paramref name="apiKey"/> accepted by
/// <see cref="DiscoverModelsAsync"/> is used solely within the outbound
/// <see cref="HttpRequestMessage"/> header scope of this method and is never
/// stored as a field, logged, or included in any thrown exception message.
/// </remarks>
public sealed class AzureOpenAIModelDiscoveryService : IAzureOpenAIModelDiscoveryService
{
    private const string ApiVersion = "2024-10-21";

    private readonly IHttpClientFactory _httpClientFactory;

    public AzureOpenAIModelDiscoveryService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> DiscoverModelsAsync(
        string endpoint,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = $"{endpoint.TrimEnd('/')}/openai/models?api-version={ApiVersion}";

        try
        {
            // A new HttpClient per call is acceptable here — IHttpClientFactory manages
            // the underlying handler pool, so per-call clients avoid header bleed
            // between concurrent workspace requests.
            using var httpClient = _httpClientFactory.CreateClient();

            // Scope the api-key header to this request only — never add it
            // to DefaultRequestHeaders on a shared or pooled client.
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("api-key", apiKey);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Discard the response body — it may contain credential echoes or
                // provider-specific diagnostic detail that must not be forwarded.
                throw new AIProviderCommunicationException(
                    $"Azure OpenAI returned HTTP {(int)response.StatusCode} when listing models.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var deployments = doc.RootElement
                .GetProperty("data")
                .EnumerateArray()
                .Select(element => element.GetProperty("id").GetString()!)
                .ToList();

            return deployments.AsReadOnly();
        }
        catch (AIProviderCommunicationException)
        {
            // Already wrapped — re-throw as-is.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Sanitised re-wrap: the inner exception is retained for diagnostics but
            // its message (which may contain endpoint URLs) must not reach the caller's
            // HTTP response body.
            throw new AIProviderCommunicationException(
                "Failed to communicate with the Azure OpenAI provider.",
                ex);
        }
    }
}
