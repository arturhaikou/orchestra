using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab;

public class GitLabApiClient : IGitLabApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitLabApiClient> _logger;
    private readonly string _projectPath;

    public GitLabApiClient(HttpClient httpClient, string apiBaseUrl, string projectPath, string apiToken, ILogger<GitLabApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _projectPath = projectPath;

        _httpClient.BaseAddress = new Uri(apiBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("PRIVATE-TOKEN", apiToken);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private string EncodedProjectPath => Uri.EscapeDataString(_projectPath);

    public async Task<(List<GitLabIssue> Issues, bool HasNextPage)> GetProjectIssuesAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v4/projects/{EncodedProjectPath}/issues?state=all&page={page}&per_page={perPage}&order_by=updated_at&sort=desc";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var issues = JsonSerializer.Deserialize<List<GitLabIssue>>(content, _jsonOptions) ?? new();

            // Use the X-Next-Page response header to determine whether more pages exist.
            // GitLab sets this header to the next page number, or leaves it empty on the last page.
            // This is reliable even when the result count equals perPage exactly.
            var hasNextPage = false;
            if (response.Headers.TryGetValues("X-Next-Page", out var nextPageValues))
            {
                var nextPageHeader = string.Join("", nextPageValues);
                hasNextPage = !string.IsNullOrWhiteSpace(nextPageHeader);
            }

            _logger.LogInformation("Retrieved {Count} issues from GitLab project {Project}", issues.Count, _projectPath);
            return (issues, hasNextPage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving issues from GitLab project {Project}", _projectPath);
            throw;
        }
    }

    public async Task<GitLabIssue?> GetIssueAsync(int iid, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v4/projects/{EncodedProjectPath}/issues/{iid}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Issue {Iid} not found in GitLab project {Project}", iid, _projectPath);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<GitLabIssue>(content, _jsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving issue {Iid} from GitLab project {Project}", iid, _projectPath);
            throw;
        }
    }

    public async Task<List<GitLabNote>> GetIssueNotesAsync(int iid, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v4/projects/{EncodedProjectPath}/issues/{iid}/notes?per_page=100&sort=asc";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<List<GitLabNote>>(content, _jsonOptions) ?? new();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error retrieving notes for issue {Iid} in GitLab project {Project}", iid, _projectPath);
            throw;
        }
    }

    public async Task<GitLabNote> AddNoteAsync(int iid, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v4/projects/{EncodedProjectPath}/issues/{iid}/notes";
            var payload = new { body };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<GitLabNote>(responseContent, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to parse note response from GitLab");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error adding note to issue {Iid} in GitLab project {Project}", iid, _projectPath);
            throw;
        }
    }

    public async Task<GitLabIssue> CreateIssueAsync(string title, string description, List<string>? labels = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v4/projects/{EncodedProjectPath}/issues";
            var payload = new
            {
                title,
                description,
                labels = labels != null && labels.Count > 0 ? string.Join(",", labels) : null
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<GitLabIssue>(responseContent, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to parse created issue response from GitLab");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error creating issue in GitLab project {Project}", _projectPath);
            throw;
        }
    }

    public async Task<GitLabMergeRequest?> GetMergeRequestAsync(int mrIid, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v4/projects/{EncodedProjectPath}/merge_requests/{mrIid}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            // Return null if the merge request is not found (404)
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            // Throw HttpRequestException for any other non-successful status
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var mergeRequest = JsonSerializer.Deserialize<GitLabMergeRequest>(content, _jsonOptions);

            _logger.LogInformation("Retrieved merge request {MrIid} from GitLab project {Project}", mrIid, _projectPath);
            return mergeRequest;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error retrieving merge request {MrIid} from project {Project}", mrIid, _projectPath);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operation cancelled while retrieving merge request {MrIid}", mrIid);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for merge request {MrIid}", mrIid);
            throw;
        }
    }

    public async Task<List<GitLabIssue>> SearchIssuesAsync(string query, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            // URL-encode the search query to prevent injection in the path
            var encodedQuery = Uri.EscapeDataString(query);

            var url = $"/api/v4/projects/{EncodedProjectPath}/issues?search={encodedQuery}&per_page={limit}&state=all";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var issues = JsonSerializer.Deserialize<List<GitLabIssue>>(content, _jsonOptions) ?? new();

            _logger.LogInformation("Found {Count} issues matching query '{Query}' in project {Project}", 
                issues.Count, query, _projectPath);
            return issues;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error searching issues for query '{Query}' in project {Project}", query, _projectPath);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operation cancelled while searching issues for query '{Query}'", query);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for search results on query '{Query}'", query);
            throw;
        }
    }

    public async Task<GitLabIssue> UpdateIssueAsync(int iid, string? title, string? description, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/api/v4/projects/{EncodedProjectPath}/issues/{iid}";

            // Build the request body with only fields that are being updated
            var body = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(title))
                body["title"] = title;
            if (description != null)
                body["description"] = description;

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var updatedIssue = JsonSerializer.Deserialize<GitLabIssue>(responseContent, _jsonOptions)!;

            _logger.LogInformation("Updated issue {IssueIid} in project {Project}", iid, _projectPath);
            return updatedIssue;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error updating issue {IssueIid} in project {Project}", iid, _projectPath);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Operation cancelled while updating issue {IssueIid}", iid);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error for updated issue {IssueIid}", iid);
            throw;
        }
    }
}
