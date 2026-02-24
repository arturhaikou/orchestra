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

            EnsureSuccessOrThrowAuthError(response);

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

            if (searchResponse != null)
            {
                // Jira On-Premise (Data Center / Server) v2 API does not include an isLast field.
                // We derive it from the total count returned by the API:
                //   isLast = startAt + fetched >= total
                var fetched = searchResponse.Tickets?.Count ?? 0;
                searchResponse.IsLast = searchResponse.Total == 0 || (searchResponse.StartAt + fetched) >= searchResponse.Total;
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

            EnsureSuccessOrThrowAuthError(response);

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

            EnsureSuccessOrThrowAuthError(response);
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

            EnsureSuccessOrThrowAuthError(response);
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

            EnsureSuccessOrThrowAuthError(response);
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
            EnsureSuccessOrThrowAuthError(response);

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
            EnsureSuccessOrThrowAuthError(response);

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

            EnsureSuccessOrThrowAuthError(response);

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
        // Check for auth/redirect issues first
        EnsureSuccessOrThrowAuthError(response, forbiddenMessage: "You do not have permission to create issues in Jira.");

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

    /// <summary>
    /// Checks the response for auth-related failures before attempting to read the body.
    /// Handles:
    /// - 3xx redirects: Jira On-Premise SSO plugins redirect unauthenticated requests to an HTML
    ///   login page. With AllowAutoRedirect=false these show up as 3xx here and produce a clear error
    ///   instead of a JsonException when the caller tries to deserialize the HTML body.
    /// - 401/403: Missing, expired, or insufficient-scope Personal Access Token.
    /// </summary>
    private void EnsureSuccessOrThrowAuthError(
        HttpResponseMessage response,
        string? forbiddenMessage = null)
    {
        var statusCode = (int)response.StatusCode;

        if (statusCode is >= 300 and < 400)
        {
            var location = response.Headers.Location?.ToString() ?? "(unknown)";
            _logger.LogError(
                "Jira On-Premise returned a redirect ({StatusCode}) to '{Location}'. " +
                "This usually means the Personal Access Token is missing, expired, or the Jira instance " +
                "requires authentication before accepting API requests.",
                statusCode, location);
            throw new InvalidOperationException(
                "Jira On-Premise redirected the request to a login page. " +
                "Please verify that a valid Personal Access Token is configured for this integration.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogError("Jira On-Premise returned 401 Unauthorized. The Personal Access Token may be missing or expired.");
            throw new InvalidOperationException(
                "Failed to authenticate with Jira On-Premise (401). " +
                "Please verify that your Personal Access Token is valid and has not expired.");
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var message = forbiddenMessage ?? "You do not have permission to perform this action in Jira On-Premise.";
            _logger.LogError("Jira On-Premise returned 403 Forbidden.");
            throw new InvalidOperationException(message);
        }
    }
}
