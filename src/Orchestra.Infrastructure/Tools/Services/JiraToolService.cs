using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;
using Orchestra.Infrastructure.Integrations.Services;
using Orchestra.Infrastructure.Tools.Models.Jira;

namespace Orchestra.Infrastructure.Tools.Services;

public class JiraToolService : IJiraToolService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ILogger<JiraToolService> _logger;
    private readonly IAdfConversionService _adfConversionService;

    public JiraToolService(
        IHttpClientFactory httpClientFactory,
        ICredentialEncryptionService credentialEncryptionService,
        IIntegrationDataAccess integrationDataAccess,
        ILogger<JiraToolService> logger,
        IAdfConversionService adfConversionService)
    {
        _httpClientFactory = httpClientFactory;
        _credentialEncryptionService = credentialEncryptionService;
        _integrationDataAccess = integrationDataAccess;
        _logger = logger;
        _adfConversionService = adfConversionService;
    }

    public async Task<object> CreateIssueAsync(
        string workspaceId,
        string summary,
        string description,
        string issueTypeName)
    {
        try
        {
            _logger.LogInformation(
                "Creating JIRA issue in workspace {WorkspaceId}: Summary='{Summary}', IssueType='{IssueType}'",
                workspaceId,
                summary,
                issueTypeName);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Step 1: Load and validate integration
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Get project ID from filter query
            var projectId = await GetProjectIdFromFilterQueryAsync(integration);

            // Step 3: Resolve issue type name to ID
            var issueTypeId = await GetIssueTypeIdAsync(integration, issueTypeName);

            // Step 4: Create the issue
            var issueKey = await CreateSingleIssueAsync(
                integration,
                projectId,
                summary,
                description,
                issueTypeId);

            // Step 5: Build success response
            var issueUrl = $"{integration.Url.TrimEnd('/')}/browse/{issueKey}";
            
            _logger.LogInformation(
                "Successfully created JIRA issue {IssueKey} in workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            return new
            {
                success = true,
                issueKey = issueKey,
                issueId = issueKey,
                url = issueUrl,
                message = $"Successfully created JIRA issue {issueKey}"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = $"Integration not found for workspace {workspaceId}",
                errorCode = "INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid integration state for workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a JIRA provider") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument for workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("validation") ? "JIRA_VALIDATION_ERROR" : "INVALID_ARGUMENT"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse JIRA API response for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "Failed to parse JIRA API response.",
                errorCode = "JIRA_PARSE_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex,
                "Failed to connect to JIRA for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "Failed to connect to JIRA. Please verify the integration URL.",
                errorCode = "JIRA_NETWORK_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("server error"))
        {
            _logger.LogError(ex,
                "JIRA server error for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "JIRA server error occurred. Please try again later.",
                errorCode = "JIRA_SERVER_ERROR"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to communicate with JIRA for workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Failed to communicate with JIRA: {ex.Message}",
                errorCode = "JIRA_HTTP_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error creating JIRA issue in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> UpdateIssueAsync(
        string workspaceId,
        string issueKey,
        string? summary = null,
        string? description = null)
    {
        try
        {
            _logger.LogInformation(
                "Updating JIRA issue {IssueKey} in workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Validate at least one field is provided
            if (string.IsNullOrEmpty(summary) && string.IsNullOrEmpty(description))
            {
                _logger.LogError(
                    "At least one field must be provided for update: summary or description");
                throw new ArgumentException(
                    "At least one field must be provided for update: summary or description");
            }

            // Step 1: Validate integration
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Get authenticated HTTP client
            using var client = GetHttpClient(integration);

            // Step 3: Build update request with only provided fields
            var updateRequest = new UpdateIssueRequest
            {
                Fields = new UpdateIssueFields()
            };

            if (!string.IsNullOrEmpty(summary))
            {
                updateRequest.Fields.Summary = summary;
            }

            if (!string.IsNullOrEmpty(description))
            {
                updateRequest.Fields.Description = await _adfConversionService.ConvertMarkdownToAdfAsync(description, CancellationToken.None);
            }

            // Step 4: PUT request to JIRA API
            var response = await client.PutAsJsonAsync(
                $"/rest/api/3/issue/{issueKey}",
                updateRequest);

            var issueUrl = $"{integration.Url.TrimEnd('/')}/browse/{issueKey}";

            // Handle specific HTTP status codes before EnsureSuccessStatusCode
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "Failed to authenticate with JIRA for workspace {WorkspaceId}",
                    workspaceId);
                throw new InvalidOperationException(
                    "Failed to authenticate with JIRA. Please verify the API key.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(
                    "Access forbidden updating JIRA issue {IssueKey} in workspace {WorkspaceId}",
                    issueKey,
                    workspaceId);
                throw new InvalidOperationException(
                    $"You do not have permission to update JIRA issue '{issueKey}'.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError(
                    "JIRA issue {IssueKey} not found in workspace {WorkspaceId}",
                    issueKey,
                    workspaceId);
                throw new InvalidOperationException(
                    $"JIRA issue '{issueKey}' not found or you do not have access to it.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "JIRA API validation error updating issue {IssueKey} in workspace {WorkspaceId}: {ErrorContent}",
                    issueKey,
                    workspaceId,
                    errorContent);

                try
                {
                    var jiraError = JsonSerializer.Deserialize<JiraErrorResponse>(errorContent);
                    var errorMessage = jiraError?.GetFormattedMessage() ?? errorContent;
                    throw new ArgumentException($"JIRA validation failed: {errorMessage}");
                }
                catch (JsonException)
                {
                    throw new ArgumentException($"JIRA validation failed: {errorContent}");
                }
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogError(
                    "JIRA server error {StatusCode} updating issue {IssueKey} in workspace {WorkspaceId}",
                    response.StatusCode,
                    issueKey,
                    workspaceId);
                throw new HttpRequestException(
                    "JIRA server error occurred. Please try again later.");
            }

            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "Successfully updated JIRA issue {IssueKey} in workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            return new
            {
                success = true,
                issueKey = issueKey,
                url = issueUrl,
                message = $"Successfully updated JIRA issue {issueKey}"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = $"Integration not found for workspace {workspaceId}",
                errorCode = "INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation while updating JIRA issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not found") ? "JIRA_NOT_FOUND" :
                           ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a JIRA provider") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument while updating JIRA issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("validation") ? "JIRA_VALIDATION_ERROR" : "INVALID_ARGUMENT"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse JIRA API response for workspace {WorkspaceId}, issue {IssueKey}",
                workspaceId,
                issueKey);
            
            return new
            {
                success = false,
                error = "Failed to parse JIRA API response.",
                errorCode = "JIRA_PARSE_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex,
                "Failed to connect to JIRA for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "Failed to connect to JIRA. Please verify the integration URL.",
                errorCode = "JIRA_NETWORK_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("server error"))
        {
            _logger.LogError(ex,
                "JIRA server error for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "JIRA server error occurred. Please try again later.",
                errorCode = "JIRA_SERVER_ERROR"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to communicate with JIRA updating issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Failed to communicate with JIRA: {ex.Message}",
                errorCode = "JIRA_HTTP_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while updating JIRA issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> DeleteIssueAsync(
        string workspaceId,
        string issueKey)
    {
        try
        {
            _logger.LogInformation(
                "Deleting JIRA issue {IssueKey} in workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Step 1: Validate integration
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Get authenticated HTTP client
            using var client = GetHttpClient(integration);

            // Step 3: DELETE request to JIRA API
            var response = await client.DeleteAsync($"/rest/api/3/issue/{issueKey}");

            // Handle specific HTTP status codes before EnsureSuccessStatusCode
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "Failed to authenticate with JIRA for workspace {WorkspaceId}",
                    workspaceId);
                throw new InvalidOperationException(
                    "Failed to authenticate with JIRA. Please verify the API key.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "JIRA API validation error deleting issue {IssueKey} in workspace {WorkspaceId}: {ErrorContent}",
                    issueKey,
                    workspaceId,
                    errorContent);

                try
                {
                    var jiraError = JsonSerializer.Deserialize<JiraErrorResponse>(errorContent);
                    var errorMessage = jiraError?.GetFormattedMessage() ?? errorContent;
                    throw new ArgumentException($"JIRA validation failed: {errorMessage}");
                }
                catch (JsonException)
                {
                    throw new ArgumentException($"JIRA validation failed: {errorContent}");
                }
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError(
                    "JIRA issue {IssueKey} not found in workspace {WorkspaceId}",
                    issueKey,
                    workspaceId);
                throw new InvalidOperationException(
                    $"JIRA issue '{issueKey}' not found or you do not have access to it.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(
                    "Permission denied deleting JIRA issue {IssueKey} in workspace {WorkspaceId}",
                    issueKey,
                    workspaceId);
                throw new InvalidOperationException(
                    $"You do not have permission to delete JIRA issue '{issueKey}'.");
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogError(
                    "JIRA server error {StatusCode} deleting issue {IssueKey} in workspace {WorkspaceId}",
                    response.StatusCode,
                    issueKey,
                    workspaceId);
                throw new HttpRequestException(
                    "JIRA server error occurred. Please try again later.");
            }

            response.EnsureSuccessStatusCode();

            // JIRA returns 204 No Content on successful deletion
            _logger.LogInformation(
                "Successfully deleted JIRA issue {IssueKey} in workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            return new
            {
                success = true,
                issueKey = issueKey,
                message = $"Successfully deleted JIRA issue {issueKey}"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = $"Integration not found for workspace {workspaceId}",
                errorCode = "INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation deleting issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not found") ? "JIRA_NOT_FOUND" :
                           ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a JIRA provider") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument deleting issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("validation") ? "JIRA_VALIDATION_ERROR" : "INVALID_ARGUMENT"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse JIRA API response for workspace {WorkspaceId}, issue {IssueKey}",
                workspaceId,
                issueKey);
            
            return new
            {
                success = false,
                error = "Failed to parse JIRA API response.",
                errorCode = "JIRA_PARSE_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex,
                "Failed to connect to JIRA for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "Failed to connect to JIRA. Please verify the integration URL.",
                errorCode = "JIRA_NETWORK_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("server error"))
        {
            _logger.LogError(ex,
                "JIRA server error for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "JIRA server error occurred. Please try again later.",
                errorCode = "JIRA_SERVER_ERROR"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to communicate with JIRA deleting issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Failed to communicate with JIRA: {ex.Message}",
                errorCode = "JIRA_HTTP_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error deleting JIRA issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> GetIssueAsync(string workspaceId, string issueKey)
    {
        try
        {
            _logger.LogInformation(
                "Retrieving JIRA issue {IssueKey} from workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Step 1: Load and validate integration
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Create authenticated HTTP client
            using var client = GetHttpClient(integration);

            // Step 3: Send GET request with explicit field selection for performance
            var fields = "summary,description,status,assignee,priority,issuetype";
            var endpoint = $"/rest/api/3/issue/{issueKey}?fields={fields}";
            
            _logger.LogDebug(
                "Fetching JIRA issue from endpoint: {Endpoint}",
                endpoint);

            var response = await client.GetAsync(endpoint);

            // Handle specific HTTP status codes before EnsureSuccessStatusCode
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "Failed to authenticate with JIRA for workspace {WorkspaceId}",
                    workspaceId);
                throw new InvalidOperationException(
                    "Failed to authenticate with JIRA. Please verify the API key.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "JIRA API validation error getting issue {IssueKey} in workspace {WorkspaceId}: {ErrorContent}",
                    issueKey,
                    workspaceId,
                    errorContent);

                try
                {
                    var jiraError = JsonSerializer.Deserialize<JiraErrorResponse>(errorContent);
                    var errorMessage = jiraError?.GetFormattedMessage() ?? errorContent;
                    throw new ArgumentException($"JIRA validation failed: {errorMessage}");
                }
                catch (JsonException)
                {
                    throw new ArgumentException($"JIRA validation failed: {errorContent}");
                }
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError(
                    "JIRA issue {IssueKey} not found in workspace {WorkspaceId}",
                    issueKey,
                    workspaceId);
                throw new InvalidOperationException(
                    $"JIRA issue '{issueKey}' not found or you do not have access to it.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(
                    "Access forbidden to JIRA issue {IssueKey} in workspace {WorkspaceId}",
                    issueKey,
                    workspaceId);
                throw new InvalidOperationException(
                    $"You do not have permission to access JIRA issue '{issueKey}'.");
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogError(
                    "JIRA server error {StatusCode} getting issue {IssueKey} in workspace {WorkspaceId}",
                    response.StatusCode,
                    issueKey,
                    workspaceId);
                throw new HttpRequestException(
                    "JIRA server error occurred. Please try again later.");
            }

            response.EnsureSuccessStatusCode();

            // Step 7: Deserialize response
            var content = await response.Content.ReadAsStringAsync();
            JiraTicket? jiraTicket;
            try
            {
                jiraTicket = JsonSerializer.Deserialize<JiraTicket>(
                    content,
                    new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize JIRA issue response for {IssueKey} in workspace {WorkspaceId}",
                    issueKey,
                    workspaceId);
                throw new InvalidOperationException(
                    "Failed to parse JIRA API response.");
            }

            if (jiraTicket == null)
            {
                throw new InvalidOperationException("Failed to parse JIRA issue response");
            }

            // Step 8: Extract fields safely and build response
            var issueUrl = $"{integration.Url.TrimEnd('/')}/browse/{issueKey}";
            var description = ExtractPlainTextFromAdf(jiraTicket.Fields?.Description);
            var assignee = ExtractAssigneeDisplayName(jiraTicket.Fields);
            var priority = jiraTicket.Fields?.Priority?.Name;
            var issueType = ExtractIssueTypeName(jiraTicket.Fields);

            _logger.LogInformation(
                "Successfully retrieved JIRA issue {IssueKey} from workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            return new
            {
                success = true,
                issueKey = jiraTicket.Key,
                summary = jiraTicket.Fields?.Summary ?? "",
                description = description,
                status = jiraTicket.Fields?.Status?.Name ?? "Unknown",
                assignee = assignee,
                priority = priority,
                issueType = issueType,
                url = issueUrl
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = $"Integration not found for workspace {workspaceId}",
                errorCode = "INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation while retrieving JIRA issue {IssueKey}: {ErrorMessage}",
                issueKey,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not found") ? "JIRA_NOT_FOUND" :
                           ex.Message.Contains("parse") ? "JIRA_PARSE_ERROR" :
                           ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a JIRA provider") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument while retrieving JIRA issue {IssueKey}: {ErrorMessage}",
                issueKey,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("validation") ? "JIRA_VALIDATION_ERROR" : "INVALID_ARGUMENT"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse JIRA API response for workspace {WorkspaceId}, issue {IssueKey}",
                workspaceId,
                issueKey);
            
            return new
            {
                success = false,
                error = "Failed to parse JIRA API response.",
                errorCode = "JIRA_PARSE_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex,
                "Failed to connect to JIRA for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "Failed to connect to JIRA. Please verify the integration URL.",
                errorCode = "JIRA_NETWORK_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("server error"))
        {
            _logger.LogError(ex,
                "JIRA server error for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "JIRA server error occurred. Please try again later.",
                errorCode = "JIRA_SERVER_ERROR"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to communicate with JIRA getting issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Failed to communicate with JIRA: {ex.Message}",
                errorCode = "JIRA_HTTP_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error getting JIRA issue {IssueKey} in workspace {WorkspaceId}: {ErrorMessage}",
                issueKey,
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
    }

    public async Task<object> CreateEpicAsync(
        string workspaceId,
        string epicTitle,
        string epicDescription,
        List<StoryRequest> stories,
        string storyTypeName = "Story")
    {
        try
        {
            _logger.LogInformation(
                "Creating JIRA epic '{EpicTitle}' with {StoryCount} stories in workspace {WorkspaceId}",
                epicTitle,
                stories.Count,
                workspaceId);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            {
                return new
                {
                    success = false,
                    error = $"Invalid GUID format for workspaceId: {workspaceId}",
                    errorCode = "INVALID_WORKSPACE_ID"
                };
            }

            // Step 1: Load and validate integration
            var integration = await GetAndValidateIntegrationAsync(workspaceGuid);

            // Step 2: Get project ID from filter query
            var projectId = await GetProjectIdFromFilterQueryAsync(integration);

            // Step 3: Resolve issue type IDs
            var epicTypeId = await GetIssueTypeIdAsync(integration, "Epic");
            var storyTypeId = await GetIssueTypeIdAsync(integration, storyTypeName);

            // Step 4: Create the epic issue
            var epicKey = await CreateSingleIssueAsync(
                integration,
                projectId,
                epicTitle,
                epicDescription,
                epicTypeId);

            var epicUrl = $"{integration.Url.TrimEnd('/')}/browse/{epicKey}";

            _logger.LogInformation(
                "Successfully created epic {EpicKey} in workspace {WorkspaceId}, now creating {StoryCount} stories",
                epicKey,
                workspaceId,
                stories.Count);

            // Step 5: Create stories under the epic
            var createdStoryKeys = new List<string>();
            foreach (var story in stories)
            {
                try
                {
                    var storyKey = await CreateSingleIssueAsync(
                        integration,
                        projectId,
                        story.Title,
                        story.Description,
                        storyTypeId,
                        parentKey: epicKey);

                    createdStoryKeys.Add(storyKey);
                    _logger.LogInformation(
                        "Successfully created story {StoryKey} under epic {EpicKey} in workspace {WorkspaceId}",
                        storyKey,
                        epicKey,
                        workspaceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to create story '{StoryTitle}' under epic {EpicKey} in workspace {WorkspaceId}: {ErrorMessage}",
                        story.Title,
                        epicKey,
                        workspaceId,
                        ex.Message);
                    // Continue with remaining stories (partial success allowed)
                }
            }

            // Step 6: Build success response
            var message = createdStoryKeys.Count == stories.Count
                ? $"Successfully created epic {epicKey} with {createdStoryKeys.Count} stories"
                : $"Created epic {epicKey} with {createdStoryKeys.Count} out of {stories.Count} stories (some failed)";

            _logger.LogInformation(
                "Epic creation completed for workspace {WorkspaceId}: {Message}",
                workspaceId,
                message);

            return new
            {
                success = true,
                epicKey = epicKey,
                epicId = epicKey,
                epicUrl = epicUrl,
                storiesCreated = createdStoryKeys.Count,
                storyKeys = createdStoryKeys,
                message = message
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex,
                "Integration not found for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = $"Integration not found for workspace {workspaceId}",
                errorCode = "INTEGRATION_NOT_FOUND"
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex,
                "Invalid operation creating epic in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("parse") ? "JIRA_PARSE_ERROR" :
                           ex.Message.Contains("not active") ? "INTEGRATION_INACTIVE" :
                           ex.Message.Contains("not a JIRA provider") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex,
                "Invalid argument creating epic in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("validation") ? "JIRA_VALIDATION_ERROR" : "INVALID_ARGUMENT"
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse JIRA API response for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "Failed to parse JIRA API response.",
                errorCode = "JIRA_PARSE_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex,
                "Failed to connect to JIRA for workspace {WorkspaceId}",
                workspaceId);
            
            return new
            {
                success = false,
                error = "Failed to connect to JIRA. Please verify the integration URL.",
                errorCode = "JIRA_NETWORK_ERROR"
            };
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("server error"))
        {
            _logger.LogError(ex,
                "JIRA server error for workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}",
                errorCode = "UNEXPECTED_ERROR"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to communicate with JIRA creating epic in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Failed to communicate with JIRA: {ex.Message}",
                errorCode = "JIRA_HTTP_ERROR"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error creating epic in workspace {WorkspaceId}: {ErrorMessage}",
                workspaceId,
                ex.Message);
            
            return new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}"
            };
        }
    }

    private HttpClient GetHttpClient(Integration integration)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();

            if (string.IsNullOrEmpty(integration.Url))
            {
                throw new ArgumentException("Integration URL is required for Jira API calls.", nameof(integration.Url));
            }
            
            if (string.IsNullOrEmpty(integration.EncryptedApiKey))
            {
                throw new ArgumentException("Integration encrypted API key is required for Jira API calls.", nameof(integration.EncryptedApiKey));
            }
            
            client.BaseAddress = new Uri(integration.Url);
            
            // Decrypt API key (format: "email:apiToken")
            var apiKey = _credentialEncryptionService.Decrypt(integration.EncryptedApiKey);
            
            // Set Basic Auth header
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{integration.Username}:{apiKey}"));
            client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", authValue);
            
            return client;
        }
        catch (UriFormatException ex)
        {
            _logger.LogError(ex, "Invalid URL format for Jira integration '{IntegrationName}': '{Url}'", 
                integration.Name, integration.Url);
            throw new ArgumentException("Invalid integration URL format.", nameof(integration.Url), ex);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is CryptographicException)
        {
            _logger.LogError(ex, "Failed to configure HttpClient for Jira integration '{IntegrationName}'", 
                integration.Name);
            throw;
        }
    }

    private async Task<Integration> GetAndValidateIntegrationAsync(
        Guid workspaceId, 
        CancellationToken cancellationToken = default)
    {
        var integrations = await _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, cancellationToken);
        var integration = integrations.FirstOrDefault(i => i.Provider == ProviderType.JIRA);
        
        if (integration == null)
        {
            _logger.LogError(
                "No active JIRA integration found for workspace {WorkspaceId}", 
                workspaceId);
            throw new InvalidOperationException(
                $"No active JIRA integration found for workspace {workspaceId}");
        }

        if (!integration.IsActive)
        {
            _logger.LogError(
                "JIRA integration {IntegrationId} for workspace {WorkspaceId} is not active", 
                integration.Id, workspaceId);
            throw new InvalidOperationException(
                $"JIRA integration for workspace {workspaceId} is not active");
        }

        return integration;
    }

    private async Task<string> GetProjectIdFromFilterQueryAsync(
        Integration integration, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = GetHttpClient(integration);

            // Build JQL query from FilterQuery (or use default if empty)
            var query = HttpUtility.ParseQueryString(string.Empty);
            var jql = !string.IsNullOrWhiteSpace(integration.FilterQuery) ? integration.FilterQuery : "ORDER BY updated DESC";
            query["jql"] = jql;
            query["fields"] = "project";
            var requestUrl = $"/rest/api/3/search/jql?{query}";
            
            _logger.LogDebug(
                "Fetching project ID from JIRA integration {IntegrationId} using JQL: {Jql}", 
                integration.Id, 
                jql);
            
            var response = await client.GetAsync(requestUrl, cancellationToken);

            // Handle specific HTTP status codes
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "Failed to authenticate with JIRA for integration {IntegrationId}",
                    integration.Id);
                throw new InvalidOperationException(
                    "Failed to authenticate with JIRA. Please verify the API key.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Invalid JQL query in FilterQuery for integration {IntegrationId}: {ErrorContent}",
                    integration.Id,
                    errorContent);
                throw new ArgumentException(
                    $"Invalid JQL query in integration FilterQuery. Please check the JQL syntax.");
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogError(
                    "JIRA server error {StatusCode} fetching project information for integration {IntegrationId}",
                    response.StatusCode,
                    integration.Id);
                throw new HttpRequestException(
                    "JIRA server error occurred. Please try again later.");
            }

            response.EnsureSuccessStatusCode();

            JiraSearchResponse? searchResponse;
            try
            {
                searchResponse = await response.Content.ReadFromJsonAsync<JiraSearchResponse>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize JIRA search result for integration {IntegrationId}",
                    integration.Id);
                throw new InvalidOperationException(
                    "Failed to parse JIRA API response when fetching project information.");
            }
            
            var projectId = searchResponse?.Tickets?.FirstOrDefault()?.Fields?.Project?.Id;
            
            if (string.IsNullOrEmpty(projectId))
            {
                _logger.LogError(
                    "No project found in FilterQuery results for integration {IntegrationId}", 
                    integration.Id);
                throw new InvalidOperationException(
                    $"No project found in FilterQuery results for integration {integration.Id}. " +
                    $"Ensure FilterQuery returns at least one issue.");
            }
            
            _logger.LogDebug(
                "Resolved project ID {ProjectId} for integration {IntegrationId}", 
                projectId, 
                integration.Id);
            
            return projectId;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, 
                "Failed to fetch project ID from JIRA integration {IntegrationId}", 
                integration.Id);
            throw;
        }
    }

    private async Task<string> GetIssueTypeIdAsync(
        Integration integration, 
        string issueTypeName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = GetHttpClient(integration);
            
            _logger.LogDebug(
                "Fetching issue type ID for '{IssueTypeName}' from JIRA integration {IntegrationId}", 
                issueTypeName, 
                integration.Id);
            
            var response = await client.GetAsync("/rest/api/3/issuetype", cancellationToken);

            // Handle specific HTTP status codes
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "Failed to authenticate with JIRA for integration {IntegrationId}",
                    integration.Id);
                throw new InvalidOperationException(
                    "Failed to authenticate with JIRA. Please verify the API key.");
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogError(
                    "JIRA server error {StatusCode} fetching issue types for integration {IntegrationId}",
                    response.StatusCode,
                    integration.Id);
                throw new HttpRequestException(
                    "JIRA server error occurred. Please try again later.");
            }

            response.EnsureSuccessStatusCode();

            List<IssueType>? issueTypes;
            try
            {
                issueTypes = await response.Content.ReadFromJsonAsync<List<IssueType>>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize JIRA issue types response for integration {IntegrationId}",
                    integration.Id);
                throw new InvalidOperationException(
                    "Failed to parse JIRA API response when fetching issue types.");
            }
            
            var issueType = issueTypes?.FirstOrDefault(it => 
                string.Equals(it.Name, issueTypeName, StringComparison.OrdinalIgnoreCase));
            
            if (issueType == null)
            {
                _logger.LogError(
                    "Issue type '{IssueTypeName}' not found in JIRA integration {IntegrationId}", 
                    issueTypeName, 
                    integration.Id);
                throw new ArgumentException(
                    $"Issue type '{issueTypeName}' not found in JIRA integration {integration.Id}. " +
                    $"Available types: {string.Join(", ", issueTypes?.Select(it => it.Name) ?? new List<string>())}");
            }
            
            _logger.LogDebug(
                "Resolved issue type '{IssueTypeName}' to ID {IssueTypeId} for integration {IntegrationId}", 
                issueTypeName, 
                issueType.Id, 
                integration.Id);
            
            return issueType.Id;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, 
                "Failed to fetch issue types from JIRA integration {IntegrationId}", 
                integration.Id);
            throw;
        }
    }

    private async Task<string> CreateSingleIssueAsync(
        Integration integration,
        string projectId,
        string summary,
        string description,
        string issueTypeId,
        string? parentKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = GetHttpClient(integration);
            
            // Build ADF-formatted description
            var adfDescription = await _adfConversionService.ConvertMarkdownToAdfAsync(description, cancellationToken);

            // Build create issue request
            var request = new CreateIssueRequest
            {
                Fields = new CreateIssueFields
                {
                    Summary = summary,
                    Description = adfDescription,
                    Issuetype = new IssueTypeField { Id = issueTypeId },
                    Project = new ProjectField { Id = projectId }
                }
            };
            
            // Add parent if provided (for subtasks/stories under epics)
            if (!string.IsNullOrEmpty(parentKey))
            {
                request.Fields.Parent = new ParentField { Key = parentKey };
            }
            
            _logger.LogDebug(
                "Creating JIRA issue in project {ProjectId} with type {IssueTypeId} for integration {IntegrationId}", 
                projectId, 
                issueTypeId, 
                integration.Id);
            
            var response = await client.PostAsJsonAsync(
                "/rest/api/3/issue", 
                request, 
                cancellationToken);
            
            // Handle specific HTTP status codes before EnsureSuccessStatusCode
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError(
                    "Failed to authenticate with JIRA for integration {IntegrationId}",
                    integration.Id);
                throw new InvalidOperationException(
                    "Failed to authenticate with JIRA. Please verify the API key.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogError(
                    "Access forbidden when creating issue in integration {IntegrationId}",
                    integration.Id);
                throw new InvalidOperationException(
                    "You do not have permission to create issues in JIRA.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "JIRA API validation error for integration {IntegrationId}: {ErrorContent}",
                    integration.Id,
                    errorContent);

                try
                {
                    var jiraError = JsonSerializer.Deserialize<JiraErrorResponse>(errorContent);
                    var errorMessage = jiraError?.GetFormattedMessage() ?? errorContent;
                    throw new ArgumentException($"JIRA validation failed: {errorMessage}");
                }
                catch (JsonException)
                {
                    throw new ArgumentException($"JIRA validation failed: {errorContent}");
                }
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogError(
                    "JIRA server error {StatusCode} for integration {IntegrationId}",
                    response.StatusCode,
                    integration.Id);
                throw new HttpRequestException(
                    "JIRA server error occurred. Please try again later.");
            }

            response.EnsureSuccessStatusCode();
            
            CreateIssueResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<CreateIssueResponse>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize JIRA issue creation response for integration {IntegrationId}",
                    integration.Id);
                throw new InvalidOperationException(
                    "Failed to parse JIRA API response after creating issue.");
            }
            
            if (result == null || string.IsNullOrEmpty(result.Key))
            {
                _logger.LogError(
                    "Failed to parse create issue response for integration {IntegrationId}", 
                    integration.Id);
                throw new InvalidOperationException("Failed to parse JIRA create issue response");
            }
            
            _logger.LogInformation(
                "Successfully created JIRA issue {IssueKey} in integration {IntegrationId}", 
                result.Key, 
                integration.Id);
            
            return result.Key;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, 
                "Failed to create issue in JIRA integration {IntegrationId}", 
                integration.Id);
            throw;
        }
    }

    /// <summary>
    /// Extracts plain text from Atlassian Document Format (ADF) content.
    /// Handles the complex nested JSON structure of ADF and returns concatenated text.
    /// </summary>
    private string ExtractPlainTextFromAdf(dynamic adfContent)
    {
        if (adfContent == null) return "";
        
        try
        {
            // Serialize dynamic object to JSON string, then deserialize to JsonElement for traversal
            var jsonString = JsonSerializer.Serialize(adfContent);
            var doc = JsonSerializer.Deserialize<JsonElement>(jsonString);
            
            // ADF structure: { "content": [ { "type": "paragraph", "content": [ { "type": "text", "text": "..." } ] } ] }
            if (doc.TryGetProperty("content", out JsonElement content))
            {
                var textParts = new List<string>();
                
                // Iterate through top-level content nodes (paragraphs, headings, etc.)
                foreach (var node in content.EnumerateArray())
                {
                    // Each node can have nested content with text
                    if (node.TryGetProperty("content", out JsonElement nodeContent))
                    {
                        foreach (var textNode in nodeContent.EnumerateArray())
                        {
                            if (textNode.TryGetProperty("text", out JsonElement text))
                            {
                                var textValue = text.GetString();
                                if (!string.IsNullOrWhiteSpace(textValue))
                                {
                                    textParts.Add(textValue);
                                }
                            }
                        }
                    }
                }
                
                return string.Join(" ", textParts);
            }
            
            return "";
        }
        catch
        {
            // Fallback: return string representation if ADF parsing fails
            return adfContent.ToString() ?? "";
        }
    }

    /// <summary>
    /// Safely extracts the display name from a JiraFields assignee property.
    /// Returns null if assignee is not assigned.
    /// </summary>
    private string? ExtractAssigneeDisplayName(JiraFields? fields)
    {
        return fields?.Assignee?.DisplayName;
    }

    /// <summary>
    /// Safely extracts the issue type name from a JiraFields issuetype property.
    /// Returns "Unknown" if issue type is not available.
    /// </summary>
    private string ExtractIssueTypeName(JiraFields? fields)
    {
        return fields?.Issuetype?.Name ?? "Unknown";
    }
}