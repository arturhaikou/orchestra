using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Integrations.Services;

/// <summary>
/// Service for converting Atlassian Document Format (ADF) to Markdown using HTTP integration.
/// </summary>
public class AdfConversionService : IAdfConversionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AdfConversionService> _logger;

    private const string HttpClientName = "adfgenerator";
    private const string AdfToMarkdownEndpoint = "/adf-to-markdown";
    private const string MarkdownToAdfEndpoint = "/";

    /// <summary>
    /// Initializes a new instance of the <see cref="AdfConversionService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating clients.</param>
    /// <param name="logger">The logger for logging operations.</param>
    public AdfConversionService(IHttpClientFactory httpClientFactory, ILogger<AdfConversionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> ConvertAdfToMarkdownAsync(JsonElement adf, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var payload = SerializeAdfPayload(adf);
        var request = new HttpRequestMessage(HttpMethod.Post, AdfToMarkdownEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        var response = await httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var markdown = DeserializeMarkdownResponse(responseJson);
        _logger.LogDebug("ADF converted to Markdown successfully. Preview: {Preview}", markdown.Length > 100 ? markdown.Substring(0, 100) + "..." : markdown);
        return markdown;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ConvertAdfBatchToMarkdownAsync(IReadOnlyList<JsonElement> adfList, CancellationToken cancellationToken)
    {
        if (adfList == null || adfList.Count == 0)
        {
            _logger.LogWarning("ConvertAdfBatchToMarkdownAsync called with null or empty list");
            return Array.Empty<string>();
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var payload = SerializeAdfBatchPayload(adfList);
        var request = new HttpRequestMessage(HttpMethod.Post, AdfToMarkdownEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        var response = await httpClient.SendAsync(request, CancellationToken.None);
        var responseJson = await response.Content.ReadAsStringAsync(CancellationToken.None);
        var markdownList = DeserializeBatchMarkdownResponse(responseJson);

        if (markdownList.Count != adfList.Count)
        {
            _logger.LogError("Batch conversion response count mismatch: expected {Expected}, got {Actual}", adfList.Count, markdownList.Count);
            throw new InvalidOperationException($"Batch conversion response count mismatch: expected {adfList.Count}, got {markdownList.Count}");
        }

        _logger.LogDebug("Batch converted {Count} ADF elements to Markdown", adfList.Count);
        return markdownList;
    }

    /// <inheritdoc/>
    public async Task<JsonElement> ConvertMarkdownToAdfAsync(string markdown, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            _logger.LogWarning("ConvertMarkdownToAdfAsync called with null or empty markdown");
            throw new ArgumentException("Markdown content cannot be null or empty.", nameof(markdown));
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var payload = JsonSerializer.Serialize(new { text = markdown });
        var request = new HttpRequestMessage(HttpMethod.Post, MarkdownToAdfEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var adfDocument = JsonDocument.Parse(responseJson);
        
        _logger.LogDebug("Markdown converted to ADF successfully. Input length: {Length}", markdown.Length);
        return adfDocument.RootElement.Clone();
    }

    private string SerializeAdfPayload(JsonElement adf)
    {
        return JsonSerializer.Serialize(adf);
    }

    private string DeserializeMarkdownResponse(string jsonResponse)
    {
        // The API returns the markdown string directly
        _logger.LogInformation("Received markdown response of length {Length}", jsonResponse.Length);
        return jsonResponse;
    }

    private string SerializeAdfBatchPayload(IReadOnlyList<JsonElement> adfList)
    {
        return JsonSerializer.Serialize(adfList);
    }

    private IReadOnlyList<string> DeserializeBatchMarkdownResponse(string jsonResponse)
    {
        var doc = JsonDocument.Parse(jsonResponse);
        var result = new List<string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            result.Add(item.GetString() ?? string.Empty);
        }
        return result;
    }
}