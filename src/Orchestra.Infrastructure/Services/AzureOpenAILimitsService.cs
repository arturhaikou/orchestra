using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Services;

public sealed class AzureOpenAILimitsService : IAzureOpenAILimitsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public AzureOpenAILimitsService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<bool> IsModelAccessibleAsync(
        string endpoint,
        string apiKey,
        string modelId,
        CancellationToken cancellationToken)
    {
        var cacheKey = ComputeCacheKey(endpoint, apiKey, modelId);

        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        var accessible = await FetchAccessibilityAsync(endpoint, apiKey, modelId, cancellationToken);

        _cache.Set(cacheKey, accessible, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl
        });

        return accessible;
    }

    private async Task<bool> FetchAccessibilityAsync(
        string endpoint,
        string apiKey,
        string modelId,
        CancellationToken cancellationToken)
    {
        var url = $"{endpoint.TrimEnd('/')}/v1/deployments/{modelId}/limits";

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("api-key", apiKey);

            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var total = doc.RootElement
                .GetProperty("minuteTokenStats")
                .GetProperty("total")
                .GetDouble();

            return total > 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return false;
        }
    }

    private static string ComputeCacheKey(string endpoint, string apiKey, string modelId)
    {
        var raw = $"{endpoint}:{apiKey}:{modelId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"azure-limits:{Convert.ToHexString(hash)}";
    }
}
