using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Web;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Infrastructure.Integrations.Providers.Jira;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;
using Orchestra.Infrastructure.Tools.Models.Jira;

namespace Orchestra.Infrastructure.Tools.Services;

public class JiraToolService : IJiraToolService
{
    private readonly JiraApiClientFactory _apiClientFactory;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly ILogger<JiraToolService> _logger;
    private readonly IJiraTextContentConverter _contentConverter;

    public JiraToolService(
        JiraApiClientFactory apiClientFactory,
        IIntegrationDataAccess integrationDataAccess,
        ILogger<JiraToolService> logger,
        IJiraTextContentConverter contentConverter)
    {
        _apiClientFactory = apiClientFactory;
        _integrationDataAccess = integrationDataAccess;
        _logger = logger;
        _contentConverter = contentConverter;
    }

    public async Task<object> CreateIssueAsync(
        string workspaceId,
        string summary,
        string description,
        string issueTypeName,
        string? projectId = null)
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

            // Step 2: Create API client
            var apiClient = _apiClientFactory.CreateClient(integration);

            // Step 3: Get project ID from parameter, filter query, or throw
            var resolvedProjectId = await GetProjectIdAsync(apiClient, integration, projectId);

            // Step 4: Resolve issue type name to ID
            var issueTypeId = await GetIssueTypeIdAsync(apiClient, issueTypeName);

            // Step 5: Create the issue
            var issueKey = await CreateSingleIssueAsync(
                apiClient,
                integration,
                resolvedProjectId,
                summary,
                description,
                issueTypeId);

            // Step 6: Build success response
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

            // Step 2: Create API client
            var apiClient = _apiClientFactory.CreateClient(integration);

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
                var convertedDescription = await _contentConverter.ConvertMarkdownToDescriptionAsync(
                    description,
                    integration.JiraType.GetValueOrDefault());
                
                // Convert object result to JsonElement if needed
                if (convertedDescription is JsonElement je)
                {
                    updateRequest.Fields.Description = je;
                }
                else
                {
                    var json = JsonSerializer.Serialize(convertedDescription);
                    updateRequest.Fields.Description = JsonSerializer.Deserialize<JsonElement>(json);
                }
            }

            // Step 4: Update issue via API client
            await apiClient.UpdateIssueAsync(issueKey, updateRequest.Fields);

            var issueUrl = $"{integration.Url.TrimEnd('/')}/browse/{issueKey}";

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

            // Step 2: Create API client
            var apiClient = _apiClientFactory.CreateClient(integration);

            // Step 3: Delete issue via API client
            await apiClient.DeleteIssueAsync(issueKey);

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

            // Step 2: Create API client
            var apiClient = _apiClientFactory.CreateClient(integration);

            // Step 3: Send request with explicit field selection for performance
            var fields = "summary,description,status,assignee,priority,issuetype";
            var jiraTicket = await apiClient.GetIssueAsync(issueKey, fields);

            if (jiraTicket == null)
            {
                return new
                {
                    success = false,
                    error = $"JIRA issue '{issueKey}' not found or you do not have access to it.",
                    errorCode = "JIRA_NOT_FOUND"
                };
            }

            // Step 4: Extract fields safely and build response
            var issueUrl = $"{integration.Url.TrimEnd('/')}/browse/{issueKey}";
            
            // Convert description from JiraTicket format to markdown
            JsonElement? descriptionElement = null;
            if (jiraTicket.Fields?.Description != null)
            {
                if (jiraTicket.Fields.Description is JsonElement je)
                {
                    descriptionElement = je;
                }
                else
                {
                    // Serialize the object to JsonElement for conversion
                    var json = JsonSerializer.Serialize(jiraTicket.Fields.Description);
                    descriptionElement = JsonSerializer.Deserialize<JsonElement>(json);
                }
            }
            
            var description = await _contentConverter.ConvertDescriptionToMarkdownAsync(
                descriptionElement ?? default,
                integration.JiraType.GetValueOrDefault());
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
        string storyTypeName = "Story",
        string? projectId = null)
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

            // Step 2: Create API client
            var apiClient = _apiClientFactory.CreateClient(integration);

            // Step 3: Get project ID from parameter, filter query, or throw
            var resolvedProjectId = await GetProjectIdAsync(apiClient, integration, projectId);

            // Step 4: Resolve issue type IDs
            var epicTypeId = await GetIssueTypeIdAsync(apiClient, "Epic");
            var storyTypeId = await GetIssueTypeIdAsync(apiClient, storyTypeName);

            // Step 5: Create the epic issue
            var epicKey = await CreateSingleIssueAsync(
                apiClient,
                integration,
                resolvedProjectId,
                epicTitle,
                epicDescription,
                epicTypeId);

            var epicUrl = $"{integration.Url.TrimEnd('/')}/browse/{epicKey}";

            _logger.LogInformation(
                "Successfully created epic {EpicKey} in workspace {WorkspaceId}, now creating {StoryCount} stories",
                epicKey,
                workspaceId,
                stories.Count);

            // Step 6: Create stories under the epic
            var createdStoryKeys = new List<string>();
            foreach (var story in stories)
            {
                try
                {
                    var storyKey = await CreateSingleIssueAsync(
                        apiClient,
                        integration,
                        resolvedProjectId,
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

            // Step 7: Build success response
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

    private async Task<string> GetProjectIdAsync(
        IJiraApiClient apiClient,
        Integration integration,
        string? projectId,
        CancellationToken cancellationToken = default)
    {
        // 1. If projectId is provided, use it
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            _logger.LogDebug("Using provided projectId: {ProjectId}", projectId);
            return projectId;
        }

        // 2. Try to extract project key from FilterQuery (JQL)
        var filterQuery = integration.FilterQuery;
        if (!string.IsNullOrWhiteSpace(filterQuery))
        {
            // Try to extract project key from JQL (e.g., project = KEY or project = "KEY")
            var match = System.Text.RegularExpressions.Regex.Match(filterQuery, "project\\s*=\\s*['\"]?(\\w+)['\"]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var projectKey = match.Groups[1].Value;
                _logger.LogDebug("Extracted project key from filter query: {ProjectKey}", projectKey);
                // Resolve project key to project ID via JIRA API
                var resolvedId = await apiClient.GetProjectIdByKeyAsync(projectKey, cancellationToken);
                if (!string.IsNullOrEmpty(resolvedId))
                {
                    return resolvedId;
                }
            }
        }

        // 3. If neither, throw
        _logger.LogError("Project ID must be specified via parameter or filter query for integration {IntegrationId}", integration.Id);
        throw new InvalidOperationException("Project ID must be specified via parameter or filter query.");
    }

    private async Task<string> GetIssueTypeIdAsync(
        IJiraApiClient apiClient,
        string issueTypeName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Fetching issue type ID for '{IssueTypeName}'", 
                issueTypeName);
            
            var issueTypes = await apiClient.GetIssueTypesAsync(cancellationToken);
            
            var issueType = issueTypes?.FirstOrDefault(it => 
                string.Equals(it.Name, issueTypeName, StringComparison.OrdinalIgnoreCase));
            
            if (issueType == null)
            {
                _logger.LogError(
                    "Issue type '{IssueTypeName}' not found", 
                    issueTypeName);
                throw new ArgumentException(
                    $"Issue type '{issueTypeName}' not found. " +
                    $"Available types: {string.Join(", ", issueTypes?.Select(it => it.Name) ?? new List<string>())}");
            }
            
            _logger.LogDebug(
                "Resolved issue type '{IssueTypeName}' to ID {IssueTypeId}", 
                issueTypeName, 
                issueType.Id);
            
            return issueType.Id;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, 
                "Failed to fetch issue types");
            throw;
        }
    }

    private async Task<string> CreateSingleIssueAsync(
        IJiraApiClient apiClient,
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
            // Build description in the appropriate format
            var descriptionBody = await _contentConverter.ConvertMarkdownToDescriptionAsync(
                description,
                integration.JiraType.GetValueOrDefault(),
                cancellationToken);

            // Build create issue request
            var request = new CreateIssueRequest
            {
                Fields = new CreateIssueFields
                {
                    Summary = summary,
                    Description = descriptionBody,
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
                "Creating JIRA issue in project {ProjectId} with type {IssueTypeId}", 
                projectId, 
                issueTypeId);
            
            var result = await apiClient.CreateIssueAsync(request, cancellationToken);
            
            if (result == null || string.IsNullOrEmpty(result.Key))
            {
                _logger.LogError(
                    "Failed to parse create issue response");
                throw new InvalidOperationException("Failed to parse JIRA create issue response");
            }
            
            _logger.LogInformation(
                "Successfully created JIRA issue {IssueKey}", 
                result.Key);
            
            return result.Key;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, 
                "Failed to create issue");
            throw;
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