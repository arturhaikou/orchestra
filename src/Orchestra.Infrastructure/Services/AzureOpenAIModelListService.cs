using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IAIModelListService"/> for the Azure OpenAI provider.
/// Parses the "ai" connection string, queries the Azure OpenAI /openai/models endpoint,
/// and returns all available deployment names.
/// </summary>
internal sealed class AzureOpenAIModelListService : IAIModelListService
{
    private const string ApiVersion = "2024-10-21";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly ILogger<AzureOpenAIModelListService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureOpenAIModelListService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating HTTP client instances.</param>
    /// <param name="configuration">The application configuration, expected to contain "ConnectionStrings:ai".</param>
    /// <param name="logger">The logger for diagnostic messages.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the "ConnectionStrings:ai" value is missing or does not contain Endpoint and Key properties.
    /// </exception>
    public AzureOpenAIModelListService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AzureOpenAIModelListService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Parse the connection string: "Endpoint=https://...;Key=abc123"
        var connectionString = configuration.GetConnectionString("ai") ?? "";
        var dict = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

        _endpoint = dict.TryGetValue("Endpoint", out var ep)
                    ? ep.TrimEnd('/')
                    : throw new InvalidOperationException(
                        "Endpoint not found in 'ai' connection string. " +
                        "Expected format: 'Endpoint=https://...;Key=...'");

        _apiKey = dict.TryGetValue("Key", out var k)
                  ? k
                  : throw new InvalidOperationException(
                      "Key not found in 'ai' connection string. " +
                      "Expected format: 'Endpoint=https://...;Key=...'");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{_endpoint}/openai/models?api-version={ApiVersion}";

            _logger.LogInformation("Fetching available Azure OpenAI deployments from {Endpoint}", _endpoint);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("api-key", _apiKey);

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<AzureModelsResponse>(
                cancellationToken: cancellationToken);

            var models = json?.Data?.Select(m => m.Id).ToList().AsReadOnly()
                         ?? (IReadOnlyList<string>)[];

            _logger.LogInformation("Successfully retrieved {ModelCount} Azure OpenAI deployments", models.Count);
            return models;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Azure OpenAI deployments from {Endpoint}", _endpoint);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving Azure OpenAI deployments");
            throw;
        }
    }

    /// <summary>
    /// Represents the JSON response structure from Azure OpenAI's /openai/models endpoint.
    /// </summary>
    private sealed record AzureModelsResponse(
        [property: JsonPropertyName("data")] List<AzureModelEntry>? Data);

    /// <summary>
    /// Represents a single model/deployment entry in the Azure OpenAI /openai/models response.
    /// Only the "id" field (deployment name) is used; other fields are ignored.
    /// </summary>
    private sealed record AzureModelEntry(
        [property: JsonPropertyName("id")] string Id);
}
