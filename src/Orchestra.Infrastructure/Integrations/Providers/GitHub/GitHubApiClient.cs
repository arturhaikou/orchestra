using System.Text;
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

    private async Task ThrowForGitHubStatusAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        // Rate limit: 429, or 403 with X-RateLimit-Remaining = 0
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
            (response.StatusCode == System.Net.HttpStatusCode.Forbidden &&
             response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) &&
             remaining.FirstOrDefault() == "0"))
        {
            throw new InvalidOperationException("GitHub API rate limit exceeded. Please try again later.");
        }

        // Auth failure: 401 or 403 (not rate-limited)
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("Failed to authenticate with GitHub. Please verify the API key.");
        }

        // Not found: 404
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("GitHub repository not found or insufficient permissions.");
        }

        // Other non-2xx
        throw new InvalidOperationException($"GitHub API error: {(int)response.StatusCode} {response.ReasonPhrase}");
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

    public async Task<GitHubIssue> GetIssueAsync(int issueNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/issues/{issueNumber}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await ThrowForGitHubStatusAsync(response, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<GitHubIssue>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("GitHub repository not found or insufficient permissions.");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, $"Network error retrieving issue {issueNumber}");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error retrieving issue {issueNumber}");
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

    /// <summary>
    /// Creates a new GitHub issue in the repository.
    /// </summary>
    /// <param name="title">The title of the issue.</param>
    /// <param name="body">The body/description of the issue.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The created GitHub issue.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
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

    /// <summary>
    /// Updates an existing GitHub issue.
    /// </summary>
    /// <param name="issueNumber">The number of the issue to update.</param>
    /// <param name="title">The new title of the issue, or null to leave unchanged.</param>
    /// <param name="body">The new body of the issue, or null to leave unchanged.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The updated GitHub issue.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    public async Task<GitHubIssue> UpdateIssueAsync(int issueNumber, string? title, string? body, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/issues/{issueNumber}";
            var payload = new Dictionary<string, object?>();

            if (title != null)
                payload["title"] = title;
            if (body != null)
                payload["body"] = body;

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = content
            };
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var issue = JsonSerializer.Deserialize<GitHubIssue>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse issue response");

            _logger.LogInformation($"Updated issue #{issueNumber} in {_owner}/{_repo}");
            return issue;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"Error updating issue {issueNumber}");
            throw;
        }
    }

    public async Task<GitHubPullRequest> GetPullRequestAsync(int pullNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/pulls/{pullNumber}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await ThrowForGitHubStatusAsync(response, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<GitHubPullRequest>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("GitHub repository not found or insufficient permissions.");
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, $"Network error retrieving pull request {pullNumber}");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, $"HTTP error retrieving pull request {pullNumber}");
            throw;
        }
    }

    public async Task<GitHubSearchResult> SearchIssuesAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        try
        {
            var encodedQuery = Uri.EscapeDataString($"{query} is:issue repo:{_owner}/{_repo}");
            var url = $"/search/issues?q={encodedQuery}&per_page={limit}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await ThrowForGitHubStatusAsync(response, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<GitHubSearchResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new GitHubSearchResult();
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, "Network error searching GitHub issues");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error searching GitHub issues");
            throw;
        }
    }

    public async Task<string> GetPullRequestDiffAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var files = await GetPullRequestFilesAsync(prNumber, cancellationToken);
            var sb = new StringBuilder();
            foreach (var file in files.Where(f => f.Patch != null))
            {
                sb.AppendLine($"diff --git a/{file.Filename} b/{file.Filename}");
                sb.AppendLine($"--- a/{file.Filename}");
                sb.AppendLine($"+++ b/{file.Filename}");
                sb.AppendLine(file.Patch);
            }
            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving diff for pull request {PrNumber} in {Owner}/{Repo}", prNumber, _owner, _repo);
            throw;
        }
    }

    public async Task<List<GitHubPullRequestFile>> GetPullRequestFilesAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/pulls/{prNumber}/files?per_page=300";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await ThrowForGitHubStatusAsync(response, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<List<GitHubPullRequestFile>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GitHubPullRequestFile>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving files for pull request {PrNumber} in {Owner}/{Repo}", prNumber, _owner, _repo);
            throw;
        }
    }

    public async Task<List<GitHubReviewComment>> GetPullRequestReviewCommentsAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/pulls/{prNumber}/comments?per_page=100";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            await ThrowForGitHubStatusAsync(response, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<List<GitHubReviewComment>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<GitHubReviewComment>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving review comments for pull request {PrNumber} in {Owner}/{Repo}", prNumber, _owner, _repo);
            throw;
        }
    }

    public async Task<GitHubReviewSubmissionResult> SubmitPullRequestReviewAsync(int prNumber, string reviewEvent, string body, IReadOnlyList<GitHubInlineReviewComment>? comments = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/pulls/{prNumber}/reviews";

            var response = await _httpClient.PostAsync(
                url,
                BuildReviewRequestContent(reviewEvent, body, comments),
                cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            // GitHub rejects REQUEST_CHANGES when the token owner is the PR author.
            // Fall back to COMMENT so the review body and inline findings are still posted.
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity
                && string.Equals(reviewEvent, "REQUEST_CHANGES", StringComparison.OrdinalIgnoreCase)
                && responseContent.Contains("own pull request", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "GitHub rejected REQUEST_CHANGES on own pull request {PrNumber}; retrying as COMMENT.",
                    prNumber);
                response = await _httpClient.PostAsync(
                    url,
                    BuildReviewRequestContent("COMMENT", body, comments),
                    cancellationToken);
                responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            }

            await ThrowForGitHubStatusAsync(response, cancellationToken);

            return JsonSerializer.Deserialize<GitHubReviewSubmissionResult>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse review submission response from GitHub");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error submitting review for pull request {PrNumber} in {Owner}/{Repo}", prNumber, _owner, _repo);
            throw;
        }
    }

    private StringContent BuildReviewRequestContent(string reviewEvent, string body, IReadOnlyList<GitHubInlineReviewComment>? comments)
    {
        string json;
        if (comments is { Count: > 0 })
        {
            var payload = new
            {
                @event = reviewEvent,
                body,
                comments = comments.Select(c => new { path = c.Path, line = c.Line, side = c.Side, body = c.Body })
            };
            json = JsonSerializer.Serialize(payload);
        }
        else
        {
            var payload = new { @event = reviewEvent, body };
            json = JsonSerializer.Serialize(payload);
        }
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    public async Task<string> GetFileContentAsync(string path, string? gitRef, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/repos/{_owner}/{_repo}/contents/{Uri.EscapeDataString(path)}";
            if (!string.IsNullOrEmpty(gitRef))
                url += $"?ref={Uri.EscapeDataString(gitRef)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            await ThrowForGitHubStatusAsync(response, cancellationToken);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var fileContent = JsonSerializer.Deserialize<GitHubFileContent>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse file content response from GitHub");

            if (fileContent.Encoding == "base64")
            {
                var decoded = Convert.FromBase64String(fileContent.Content.Replace("\n", ""));
                return Encoding.UTF8.GetString(decoded);
            }

            return fileContent.Content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving file content for path {Path} in {Owner}/{Repo}", path, _owner, _repo);
            throw;
        }
    }
}
