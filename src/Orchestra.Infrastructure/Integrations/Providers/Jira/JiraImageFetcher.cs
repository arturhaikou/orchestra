using Microsoft.Extensions.Logging;
using Orchestra.Application.Agents.Models;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Integrations.Providers.Jira;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

/// <summary>
/// Fetches Jira attachment images in-memory using authenticated API calls so that
/// the agent runtime can pass them as binary DataContent to the LLM.
/// Images are never persisted to disk or the database.
/// </summary>
public class JiraImageFetcher
{
    private readonly JiraApiClientFactory _apiClientFactory;
    private readonly ILogger<JiraImageFetcher> _logger;

    public JiraImageFetcher(
        JiraApiClientFactory apiClientFactory,
        ILogger<JiraImageFetcher> logger)
    {
        _apiClientFactory = apiClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the bytes of each image URL in parallel and returns the results as
    /// <see cref="AgentImageRef"/> records with the <c>Bytes</c> field populated.
    /// URLs that cannot be fetched are logged and skipped — they will not appear
    /// in the returned list.
    /// </summary>
    /// <param name="integration">The Jira integration used for authentication.</param>
    /// <param name="imageUrls">Distinct list of image URLs to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<AgentImageRef>> FetchAsync(
        Integration integration,
        IReadOnlyList<string> imageUrls,
        CancellationToken cancellationToken = default)
    {
        if (imageUrls.Count == 0)
            return [];

        var apiClient = _apiClientFactory.CreateClient(integration);

        var tasks = imageUrls.Select(url => FetchSingleAsync(apiClient, url, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.Where(r => r is not null).Cast<AgentImageRef>().ToList();
    }

    private async Task<AgentImageRef?> FetchSingleAsync(
        IJiraApiClient apiClient,
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            var (data, mimeType) = await apiClient.GetAttachmentContentAsync(url, cancellationToken);
            var fileName = ExtractFileName(url);
            return new AgentImageRef(url, mimeType, fileName, data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Jira attachment image from {Url}; skipping", url);
            return null;
        }
    }

    private static string ExtractFileName(string url)
    {
        try
        {
            var lastSegment = url.TrimEnd('/').Split('/').Last();
            return string.IsNullOrWhiteSpace(lastSegment) ? "image" : lastSegment;
        }
        catch
        {
            return "image";
        }
    }
}
