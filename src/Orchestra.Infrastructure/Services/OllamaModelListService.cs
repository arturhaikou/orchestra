using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Implements <see cref="IAIModelListService"/> for the Ollama AI provider.
/// Queries the Ollama container's /api/tags endpoint to retrieve all available model names.
/// </summary>
internal sealed class OllamaModelListService : IAIModelListService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _baseUrl;
    private readonly ILogger<OllamaModelListService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaModelListService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating HTTP client instances.</param>
    /// <param name="configuration">The application configuration, expected to contain "Ollama:BaseUrl".</param>
    /// <param name="logger">The logger for diagnostic messages.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the "Ollama:BaseUrl" configuration key is not present.
    /// </exception>
    public OllamaModelListService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaModelListService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = configuration["Ollama:BaseUrl"]
                   ?? throw new InvalidOperationException(
                       "Ollama:BaseUrl configuration is required for OllamaModelListService.");
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{_baseUrl.TrimEnd('/')}/api/tags";

            _logger.LogInformation("Fetching available Ollama models from {Url}", url);

            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
                cancellationToken: cancellationToken);

            var models = json?.Models?.Select(m => m.Name).ToList().AsReadOnly()
                         ?? (IReadOnlyList<string>)[];

            _logger.LogInformation("Successfully retrieved {ModelCount} Ollama models", models.Count);
            return models;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Ollama models from {BaseUrl}", _baseUrl);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving Ollama models");
            throw;
        }
    }

    /// <summary>
    /// Represents the JSON response structure from Ollama's /api/tags endpoint.
    /// </summary>
    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")] List<OllamaModelEntry>? Models);

    /// <summary>
    /// Represents a single model entry in the Ollama /api/tags response.
    /// Only the "name" field is used; other fields are ignored.
    /// </summary>
    private sealed record OllamaModelEntry(
        [property: JsonPropertyName("name")] string Name);
}
