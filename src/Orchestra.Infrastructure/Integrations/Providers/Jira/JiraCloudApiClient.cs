using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

/// <summary>
/// Jira Cloud API (v3) client implementation.
/// Handles API calls to Atlassian Cloud instances using REST API v3 endpoints.
/// </summary>
public class JiraCloudApiClient : IJiraApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraCloudApiClient> _logger;

    public JiraCloudApiClient(HttpClient httpClient, ILogger<JiraCloudApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<JiraSearchResponse> SearchIssuesAsync(
        string jql,
        string fields,
        int startAt = 0,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["jql"] = jql;
            query["fields"] = fields;
            query["startAt"] = startAt.ToString();
            query["maxResults"] = maxResults.ToString();

            var requestUrl = $"rest/api/3/search/jql?{query}";
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
                _logger.LogError(ex, "Failed to deserialize search response from Jira Cloud: {ResponseContent}", content);
                throw;
            }

            return searchResponse ?? new JiraSearchResponse { Tickets = new List<JiraTicket>(), IsLast = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search issues in Jira Cloud: JQL={Jql}, StartAt={StartAt}, MaxResults={MaxResults}",
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
                $"/rest/api/3/issue/{issueKey}?fields={fields}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Jira issue {IssueKey} not found in Cloud API", issueKey);
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
            _logger.LogError(ex, "Failed to get issue {IssueKey} from Jira Cloud", issueKey);
            throw;
        }
    }

    public async Task<CreateIssueResponse> CreateIssueAsync(
        CreateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/rest/api/3/issue",
                request,
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
            _logger.LogError(ex, "Failed to create issue in Jira Cloud: Summary='{Summary}'", request.Fields?.Summary);
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
                $"/rest/api/3/issue/{issueKey}",
                jsonContent,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update issue {IssueKey} in Jira Cloud", issueKey);
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
                $"/rest/api/3/issue/{issueKey}",
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete issue {IssueKey} from Jira Cloud", issueKey);
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
                $"/rest/api/3/issue/{issueKey}/comment",
                jsonContent,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add comment to issue {IssueKey} in Jira Cloud", issueKey);
            throw;
        }
    }

    public async Task<List<IssueType>> GetIssueTypesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/rest/api/3/issuetype", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var issueTypes = JsonSerializer.Deserialize<List<IssueType>>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return issueTypes ?? new List<IssueType>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get issue types from Jira Cloud");
            throw;
        }
    }

    private void HandleCreateIssueErrors(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogError("Failed to authenticate with Jira Cloud API");
            throw new InvalidOperationException("Failed to authenticate with Jira. Please verify the API key.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            _logger.LogError("Access forbidden when creating issue in Jira Cloud");
            throw new InvalidOperationException("You do not have permission to create issues in Jira.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogError("Jira Cloud API validation error: {StatusCode}", response.StatusCode);
            throw new ArgumentException("Jira validation failed");
        }

        if ((int)response.StatusCode >= 500)
        {
            _logger.LogError("Jira Cloud server error: {StatusCode}", response.StatusCode);
            throw new HttpRequestException("Jira server error occurred. Please try again later.");
        }

        response.EnsureSuccessStatusCode();
    }
}
