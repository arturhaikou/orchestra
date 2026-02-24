using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestra.Domain.Entities;
using Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub;

public class GitHubApiClient : IGitHubApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubApiClient> _logger;
    private readonly string _owner;
    private readonly string _repo;
    private const string GitHubBaseUrl = "https://api.github.com";

    public GitHubApiClient(HttpClient httpClient, string owner, string repo, string apiToken, ILogger<GitHubApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _owner = owner;
        _repo = repo;

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(GitHubBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Orchestra-GitHub-Integration");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
    }

    public async Task<(List<GitHubIssue> Issues, bool HasNextPage)> GetRepositoryIssuesAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/issues?state=all&page={page}&per_page={perPage}&sort=updated&direction=desc";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var issues = JsonSerializer.Deserialize<List<GitHubIssue>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();

            // Use the Link header to determine if a next page exists.
            // GitHub omits the Link header (or omits rel="next") on the final page,
            // making this reliable even when the result count equals perPage exactly.
            var hasNextPage = false;
            if (response.Headers.TryGetValues("Link", out var linkValues))
            {
                var linkHeader = string.Join(",", linkValues);
                hasNextPage = linkHeader.Contains("rel=\"next\"");
            }

            _logger.LogInformation($"Retrieved {issues.Count} issues from GitHub repository {_owner}/{_repo}");
            return (issues, hasNextPage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error retrieving issues from GitHub repository {_owner}/{_repo}");
            throw;
        }
    }

    public async Task<GitHubIssue?> GetIssueAsync(int issueNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/issues/{issueNumber}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Issue {issueNumber} not found in {_owner}/{_repo}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var issue = JsonSerializer.Deserialize<GitHubIssue>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return issue;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error retrieving issue {issueNumber} from GitHub");
            throw;
        }
    }

    public async Task<List<GitHubComment>> GetIssueCommentsAsync(int issueNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/issues/{issueNumber}/comments?per_page=100";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var comments = JsonSerializer.Deserialize<List<GitHubComment>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();

            return comments;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error retrieving comments for issue {issueNumber}");
            throw;
        }
    }

    public async Task<GitHubComment> AddCommentAsync(int issueNumber, string commentBody, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/issues/{issueNumber}/comments";
            var payload = new { body = commentBody };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var comment = JsonSerializer.Deserialize<GitHubComment>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse comment response");

            _logger.LogInformation($"Added comment to issue {issueNumber}");
            return comment;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error adding comment to issue {issueNumber}");
            throw;
        }
    }

    public async Task<GitHubIssue> CreateIssueAsync(string title, string body, List<string>? labels = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/issues";
            var payload = new
            {
                title,
                body,
                labels = labels ?? new List<string>()
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var issue = JsonSerializer.Deserialize<GitHubIssue>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse issue response");

            _logger.LogInformation($"Created issue #{issue.Number} in {_owner}/{_repo}");
            return issue;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error creating issue in {_owner}/{_repo}");
            throw;
        }
    }
}
