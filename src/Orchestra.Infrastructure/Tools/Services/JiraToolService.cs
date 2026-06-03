using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Web;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Domain.Utilities;
using Orchestra.Infrastructure.Integrations.Providers.Jira;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;
using Orchestra.Infrastructure.Tools.Models;
using Orchestra.Infrastructure.Tools.Models.Jira;

namespace Orchestra.Infrastructure.Tools.Services;

public class JiraToolService : IJiraToolService
{
    private readonly JiraApiClientFactory _apiClientFactory;
    private readonly IIntegrationResolver _integrationResolver;
    private readonly ILogger<JiraToolService> _logger;
    private readonly IJiraTextContentConverter _contentConverter;
    private readonly IJiraRichContentBuilder _richContentBuilder;

    public JiraToolService(
        JiraApiClientFactory apiClientFactory,
        IIntegrationResolver integrationResolver,
        ILogger<JiraToolService> logger,
        IJiraTextContentConverter contentConverter,
        IJiraRichContentBuilder richContentBuilder)
    {
        _apiClientFactory = apiClientFactory;
        _integrationResolver = integrationResolver;
        _logger = logger;
        _contentConverter = contentConverter;
        _richContentBuilder = richContentBuilder;
    }

    public async Task<object> CreateIssueAsync(
        string workspaceId,
        string integrationId,
        string summary,
        string issueTypeName,
        List<ContentBlock> descriptionBlocks)
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

            // Step 1: Validate image paths before making any API calls
            ValidateImagePaths(descriptionBlocks);

            // Step 2: Load and validate integration
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.JIRA);

            // Step 3: Create API client
            var apiClient = _apiClientFactory.CreateClient(integration);

            // Step 4: Get project ID from filter query or throw
            var resolvedProjectId = await GetProjectIdAsync(apiClient, integration);

            // Step 5: Resolve issue type name to ID
            var issueTypeId = await GetIssueTypeIdAsync(apiClient, issueTypeName);

            // Step 6: Create the issue
            var issueKey = await CreateSingleIssueAsync(
                apiClient,
                integration,
                resolvedProjectId,
                summary,
                string.Empty,
                issueTypeId,
                descriptionBlocks: descriptionBlocks);

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
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not a Jira integration") ? "INTEGRATION_WRONG_PROVIDER" :
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
        string integrationId,
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
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.JIRA);

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
                var jiraType = IntegrationTypeDetector.DetectJiraType(integration.Url);

                if (jiraType == JiraType.Cloud && _richContentBuilder.ContainsLocalImageRefs(description))
                {
                    updateRequest.Fields.Description = await _richContentBuilder.BuildAdfAsync(apiClient, issueKey, description);
                }
                else
                {
                    var convertedDescription = await _contentConverter.ConvertMarkdownToDescriptionAsync(description, jiraType);

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
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not found") ? "JIRA_NOT_FOUND" :
                           ex.Message.Contains("not a Jira integration") ? "INTEGRATION_WRONG_PROVIDER" :
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
        string integrationId,
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
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.JIRA);

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
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not found") ? "JIRA_NOT_FOUND" :
                           ex.Message.Contains("not a Jira integration") ? "INTEGRATION_WRONG_PROVIDER" :
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

    public async Task<object> GetIssueAsync(string workspaceId, string integrationId, string issueKey)
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
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.JIRA);

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
                IntegrationTypeDetector.DetectJiraType(integration.Url));
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
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("not found") ? "JIRA_NOT_FOUND" :
                           ex.Message.Contains("parse") ? "JIRA_PARSE_ERROR" :
                           ex.Message.Contains("not a Jira integration") ? "INTEGRATION_WRONG_PROVIDER" :
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
        string integrationId,
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
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.JIRA);

            // Step 2: Create API client
            var apiClient = _apiClientFactory.CreateClient(integration);

            // Step 3: Get project ID from filter query or throw
            var resolvedProjectId = await GetProjectIdAsync(apiClient, integration);

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
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found for the supplied ID") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("authenticate") ? "JIRA_AUTH_FAILED" :
                           ex.Message.Contains("permission") ? "JIRA_FORBIDDEN" :
                           ex.Message.Contains("parse") ? "JIRA_PARSE_ERROR" :
                           ex.Message.Contains("not a Jira integration") ? "INTEGRATION_WRONG_PROVIDER" :
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

    public async Task<object> AddCommentAsync(
        string workspaceId,
        string integrationId,
        string issueKey,
        List<ContentBlock> contentBlocks)
    {
        try
        {
            _logger.LogInformation(
                "Adding comment to JIRA issue {IssueKey} in workspace {WorkspaceId}",
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

            if (contentBlocks == null || contentBlocks.Count == 0)
            {
                return new
                {
                    success = false,
                    error = "contentBlocks must contain at least one block",
                    errorCode = "INVALID_ARGUMENT"
                };
            }

            ValidateImagePaths(contentBlocks);

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.JIRA);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var jiraType = IntegrationTypeDetector.DetectJiraType(integration.Url);

            if (jiraType == JiraType.Cloud)
            {
                var adfBody = await _richContentBuilder.BuildAdfFromBlocksAsync(apiClient, issueKey, contentBlocks);
                await apiClient.AddCommentAsync(issueKey, adfBody);
            }
            else
            {
                var textParts = new List<string>();
                var attachedNames = new List<string>();

                foreach (var block in contentBlocks)
                {
                    if (block.Type == "text")
                    {
                        textParts.Add(block.Content);
                    }
                    else if (block.Type == "image")
                    {
                        var filePath = block.Content;
                        if (!filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                            !filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        {
                            var localPath = NormalizeLocalFilePath(filePath);
                            var fileName = block.FileName ?? Path.GetFileName(localPath);
                            var mimeType = Path.GetExtension(fileName).ToLowerInvariant() switch
                            {
                                ".jpg" or ".jpeg" => "image/jpeg",
                                ".gif"            => "image/gif",
                                ".svg"            => "image/svg+xml",
                                ".webp"           => "image/webp",
                                _                 => "image/png"
                            };
                            await using var fileStream = File.OpenRead(localPath);
                            await apiClient.UploadAttachmentAsync(issueKey, fileStream, fileName, mimeType);
                            attachedNames.Add(fileName);
                        }
                    }
                }

                var commentText = string.Join("\n\n", textParts);
                if (attachedNames.Count > 0)
                    commentText += $"\n\n*Attached images: {string.Join(", ", attachedNames)}*";

                var commentBody = await _contentConverter.ConvertMarkdownToCommentBodyAsync(commentText, jiraType);
                await apiClient.AddCommentAsync(issueKey, commentBody);
            }

            var issueUrl = $"{integration.Url.TrimEnd('/')}/browse/{issueKey}";

            _logger.LogInformation(
                "Successfully added comment to JIRA issue {IssueKey} in workspace {WorkspaceId}",
                issueKey,
                workspaceId);

            return new
            {
                success = true,
                issueKey = issueKey,
                url = issueUrl,
                message = $"Successfully added comment to JIRA issue {issueKey}"
            };
        }
        catch (IntegrationNotFoundException ex)
        {
            _logger.LogError(ex, "Integration not found for workspace {WorkspaceId}", workspaceId);
            return new { success = false, error = $"Integration not found for workspace {workspaceId}", errorCode = "INTEGRATION_NOT_FOUND" };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation adding comment to {IssueKey}: {ErrorMessage}", issueKey, ex.Message);
            return new
            {
                success = false,
                error = ex.Message,
                errorCode = ex.Message.Contains("integrationId is required") ? "INTEGRATION_ID_REQUIRED" :
                           ex.Message.Contains("No active integration found") ? "INTEGRATION_NOT_FOUND" :
                           ex.Message.Contains("not a Jira integration") ? "INTEGRATION_WRONG_PROVIDER" :
                           "INVALID_OPERATION"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid argument adding comment to {IssueKey}: {ErrorMessage}", issueKey, ex.Message);
            return new { success = false, error = ex.Message, errorCode = "INVALID_ARGUMENT" };
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Image file not found: {ErrorMessage}", ex.Message);
            return new { success = false, error = $"Image file not found: {ex.FileName}", errorCode = "FILE_NOT_FOUND" };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, "Failed to connect to JIRA for workspace {WorkspaceId}", workspaceId);
            return new { success = false, error = "Failed to connect to JIRA. Please verify the integration URL.", errorCode = "JIRA_NETWORK_ERROR" };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to communicate with JIRA adding comment to {IssueKey}: {ErrorMessage}", issueKey, ex.Message);
            return new { success = false, error = $"Failed to communicate with JIRA: {ex.Message}", errorCode = "JIRA_HTTP_ERROR" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding comment to JIRA issue {IssueKey}: {ErrorMessage}", issueKey, ex.Message);
            return new { success = false, error = $"Unexpected error: {ex.Message}", errorCode = "UNEXPECTED_ERROR" };
        }
    }

    private async Task<string> GetProjectIdAsync(
        IJiraApiClient apiClient,
        Integration integration,
        CancellationToken cancellationToken = default)
    {
        var filterQuery = integration.FilterQuery;
        if (!string.IsNullOrWhiteSpace(filterQuery))
        {
            var match = System.Text.RegularExpressions.Regex.Match(filterQuery, "project\\s*=\\s*['\"]?(\\w+)['\"]?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var projectKey = match.Groups[1].Value;
                _logger.LogDebug("Extracted project key from filter query: {ProjectKey}", projectKey);
                var resolvedId = await apiClient.GetProjectIdByKeyAsync(projectKey, cancellationToken);
                if (!string.IsNullOrEmpty(resolvedId))
                {
                    return resolvedId;
                }
            }
        }

        _logger.LogError("Could not resolve project for integration {IntegrationId}", integration.Id);
        throw new InvalidOperationException("Project must be specified in the integration's filter query (e.g. project = PROJ).");
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
        IReadOnlyList<ContentBlock>? descriptionBlocks = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jiraType = IntegrationTypeDetector.DetectJiraType(integration.Url);

            object descriptionForCreate;
            bool needsBlocksUpdate = false;

            if (descriptionBlocks != null && descriptionBlocks.Count > 0 && jiraType == JiraType.Cloud)
            {
                bool hasFilePathImages = descriptionBlocks.Any(b =>
                    b.Type == "image" &&
                    !b.Content.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !b.Content.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

                if (hasFilePathImages)
                {
                    // Create with text-only first; update description with images after we have the issue key
                    var textOnly = string.Join("\n\n", descriptionBlocks
                        .Where(b => b.Type == "text")
                        .Select(b => b.Content));
                    descriptionForCreate = await _contentConverter.ConvertMarkdownToDescriptionAsync(
                        textOnly, jiraType, cancellationToken);
                    needsBlocksUpdate = true;
                }
                else
                {
                    // All images are external URLs — build ADF directly
                    descriptionForCreate = await _richContentBuilder.BuildAdfFromBlocksAsync(
                        apiClient, string.Empty, descriptionBlocks, cancellationToken);
                }
            }
            else if (descriptionBlocks != null && descriptionBlocks.Count > 0)
            {
                // OnPremise: join text blocks as plain description; images are uploaded as attachments after creation
                var textOnly = string.Join("\n\n", descriptionBlocks
                    .Where(b => b.Type == "text")
                    .Select(b => b.Content));
                descriptionForCreate = await _contentConverter.ConvertMarkdownToDescriptionAsync(
                    textOnly, jiraType, cancellationToken);
                needsBlocksUpdate = true;
            }
            else
            {
                var hasLocalImages = jiraType == JiraType.Cloud && _richContentBuilder.ContainsLocalImageRefs(description);

                if (hasLocalImages)
                {
                    descriptionForCreate = await _contentConverter.ConvertMarkdownToDescriptionAsync(
                        _richContentBuilder.StripLocalImageRefs(description), jiraType, cancellationToken);
                    needsBlocksUpdate = false; // handled below via legacy path
                }
                else
                {
                    descriptionForCreate = await _contentConverter.ConvertMarkdownToDescriptionAsync(
                        description, jiraType, cancellationToken);
                }

                // Legacy file:// image path — mark for post-create update
                if (jiraType == JiraType.Cloud && _richContentBuilder.ContainsLocalImageRefs(description))
                    needsBlocksUpdate = true;
            }

            var request = new CreateIssueRequest
            {
                Fields = new CreateIssueFields
                {
                    Summary = summary,
                    Description = descriptionForCreate,
                    Issuetype = new IssueTypeField { Id = issueTypeId },
                    Project = new ProjectField { Id = projectId }
                }
            };

            if (!string.IsNullOrEmpty(parentKey))
                request.Fields.Parent = new ParentField { Key = parentKey };

            _logger.LogDebug(
                "Creating JIRA issue in project {ProjectId} with type {IssueTypeId}",
                projectId,
                issueTypeId);

            var result = await apiClient.CreateIssueAsync(request, cancellationToken);

            if (result == null || string.IsNullOrEmpty(result.Key))
            {
                _logger.LogError("Failed to parse create issue response");
                throw new InvalidOperationException("Failed to parse JIRA create issue response");
            }

            if (needsBlocksUpdate)
            {
                if (jiraType == JiraType.Cloud)
                {
                    var adf = descriptionBlocks != null
                        ? await _richContentBuilder.BuildAdfFromBlocksAsync(apiClient, result.Key, descriptionBlocks, cancellationToken)
                        : await _richContentBuilder.BuildAdfAsync(apiClient, result.Key, description, cancellationToken);
                    await apiClient.UpdateIssueAsync(result.Key, new { description = adf }, cancellationToken);
                }
                else if (descriptionBlocks != null)
                {
                    // OnPremise: upload local file-path images as attachments
                    foreach (var block in descriptionBlocks)
                    {
                        if (block.Type != "image") continue;
                        var filePath = block.Content;
                        if (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var localPath = NormalizeLocalFilePath(filePath);
                        var fileName = block.FileName ?? Path.GetFileName(localPath);
                        var mimeType = Path.GetExtension(fileName).ToLowerInvariant() switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".gif"            => "image/gif",
                            ".svg"            => "image/svg+xml",
                            ".webp"           => "image/webp",
                            _                 => "image/png"
                        };
                        await using var fileStream = File.OpenRead(localPath);
                        await apiClient.UploadAttachmentAsync(result.Key, fileStream, fileName, mimeType, cancellationToken);
                    }
                }
            }

            _logger.LogInformation("Successfully created JIRA issue {IssueKey}", result.Key);

            return result.Key;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create issue");
            throw;
        }
    }

    private static string NormalizeLocalFilePath(string path) =>
        path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
            ? path[8..]
            : path.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                ? path[7..]
                : path;

    private static void ValidateImagePaths(IEnumerable<ContentBlock>? blocks)
    {
        if (blocks == null) return;
        foreach (var block in blocks)
        {
            if (block.Type != "image") continue;
            var content = block.Content;
            if (content.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                content.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                continue;
            var localPath = NormalizeLocalFilePath(content);
            if (!Path.IsPathRooted(localPath))
                throw new ArgumentException(
                    $"Image path must be absolute. Received relative path: '{content}'. Please provide the full absolute path (e.g. C:\\Users\\...\\image.png).");
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