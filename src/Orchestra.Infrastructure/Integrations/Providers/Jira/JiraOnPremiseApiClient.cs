using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

/// <summary>
/// Jira On-Premise API (v2) client implementation.
/// Handles API calls to self-hosted or data center Jira instances using REST API v2 endpoints.
/// </summary>
public class JiraOnPremiseApiClient : IJiraApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraOnPremiseApiClient> _logger;

    public JiraOnPremiseApiClient(HttpClient httpClient, ILogger<JiraOnPremiseApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<JiraSearchResponse> SearchIssuesAsync(
        string jql,
        string fields,
        int startAt = 0,
        int maxResults = 50,
        string? nextPageToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // On-Premise Jira does not support nextPageToken, so we ignore it and use startAt/maxResults
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["jql"] = jql;
            query["fields"] = fields;
            query["startAt"] = startAt.ToString();
            query["maxResults"] = maxResults.ToString();

            var requestUrl = $"rest/api/2/search?{query}";
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            JiraSearchResponse searchResponse;
            try
            {
                searchResponse = JsonSerializer.Deserialize<JiraSearchResponse>(
                    content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize search response from Jira On-Premise: {ResponseContent}", content);
                throw;
            }

            return searchResponse ?? new JiraSearchResponse { Tickets = new List<JiraTicket>(), IsLast = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search issues in Jira On-Premise: JQL={Jql}, StartAt={StartAt}, MaxResults={MaxResults}",
                jql, startAt, maxResults);
            throw;
        }
    }

    public async Task<JiraTicket?> GetIssueAsync(
        string issueKey,
        string fields,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/rest/api/2/issue/{issueKey}?fields={fields}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Jira issue {IssueKey} not found in On-Premise API", issueKey);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jiraTicket = JsonSerializer.Deserialize<JiraTicket>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return jiraTicket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get issue {IssueKey} from Jira On-Premise", issueKey);
            throw;
        }
    }

    public async Task<CreateIssueResponse> CreateIssueAsync(
        CreateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // For v2, wrap fields in the request
            var v2Request = new { fields = request.Fields };

            var response = await _httpClient.PostAsJsonAsync(
                "/rest/api/2/issue",
                v2Request,
                cancellationToken: cancellationToken);

            HandleCreateIssueErrors(response);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var createResponse = JsonSerializer.Deserialize<CreateIssueResponse>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return createResponse ?? throw new InvalidOperationException("Failed to parse create issue response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create issue in Jira On-Premise: Summary='{Summary}'", request.Fields?.Summary);
            throw;
        }
    }

    public async Task UpdateIssueAsync(
        string issueKey,
        object fields,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(new { fields }),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PutAsync(
                $"/rest/api/2/issue/{issueKey}",
                jsonContent,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update issue {IssueKey} in Jira On-Premise", issueKey);
            throw;
        }
    }

    public async Task DeleteIssueAsync(
        string issueKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/rest/api/2/issue/{issueKey}",
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete issue {IssueKey} from Jira On-Premise", issueKey);
            throw;
        }
    }

    public async Task AddCommentAsync(
        string issueKey,
        object body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(new { body }),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/rest/api/2/issue/{issueKey}/comment",
                jsonContent,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add comment to issue {IssueKey} in Jira On-Premise", issueKey);
            throw;
        }
    }

    public async Task<List<IssueType>> GetIssueTypesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            // v2 API uses /issue/createmeta for issue types, but this requires projectKeys
            // Fallback to global issue types endpoint if available
            var response = await _httpClient.GetAsync("/rest/api/2/issuetype", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var issueTypes = JsonSerializer.Deserialize<List<IssueType>>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return issueTypes ?? new List<IssueType>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get issue types from Jira On-Premise");
            throw;
        }
    }

    public async Task<List<Project>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/rest/api/2/project", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // On-Premise v2 API returns a direct array of projects
            var projects = JsonSerializer.Deserialize<List<Project>>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Project>();

            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get projects from Jira On-Premise");
            throw;
        }
    }

    public async Task<string?> GetProjectIdByKeyAsync(
        string projectKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/rest/api/2/project/{projectKey}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Project {ProjectKey} not found in Jira On-Premise", projectKey);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("id", out var idElement))
            {
                return idElement.GetString();
            }

            _logger.LogWarning("Project {ProjectKey} response missing 'id' field in Jira On-Premise", projectKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get project {ProjectKey} from Jira On-Premise", projectKey);
            throw;
        }
    }

    private void HandleCreateIssueErrors(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogError("Failed to authenticate with Jira On-Premise API");
            throw new InvalidOperationException("Failed to authenticate with Jira. Please verify the API key.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogError("Access forbidden when creating issue in Jira On-Premise");
            throw new InvalidOperationException("You do not have permission to create issues in Jira.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogError("Jira On-Premise API validation error: {StatusCode}", response.StatusCode);
            throw new ArgumentException("Jira validation failed");
        }

        if ((int)response.StatusCode >= 500)
        {
            _logger.LogError("Jira On-Premise server error: {StatusCode}", response.StatusCode);
            throw new HttpRequestException("Jira server error occurred. Please try again later.");
        }

        response.EnsureSuccessStatusCode();
    }
}
