using System.Web;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.Services;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;
using Orchestra.Domain.Utilities;
using Orchestra.Infrastructure.Integrations.Providers.Jira.Models;
using System.Text.Json;
using System.Net;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira;

public class JiraTicketProvider : ITicketProvider
{
    private readonly JiraApiClientFactory _apiClientFactory;
    private readonly ILogger<JiraTicketProvider> _logger;
    private readonly IJiraTextContentConverter _contentConverter;

    public JiraTicketProvider(
        JiraApiClientFactory apiClientFactory,
        ILogger<JiraTicketProvider> logger,
        IJiraTextContentConverter contentConverter)
    {
        _apiClientFactory = apiClientFactory;
        _logger = logger;
        _contentConverter = contentConverter;
    }

    public async Task<(List<ExternalTicketDto> Tickets, bool IsLast, string? NextPageToken)> 
        FetchTicketsAsync(
            Integration integration,
            int startAt = 0,
            int maxResults = 50,
            string? pageToken = null,
            CancellationToken cancellationToken = default)
    {
        var apiClient = _apiClientFactory.CreateClient(integration);
        var filter = integration.FilterQuery;
        var jql = !string.IsNullOrWhiteSpace(filter) 
            ? $"{filter} ORDER BY priority DESC, updated DESC" 
            : "ORDER BY priority DESC, updated DESC";
        try
        {
            var fields = "key,status,priority,summary,description,comment,created,updated";
            var searchResponse = await apiClient.SearchIssuesAsync(
                jql,
                fields,
                startAt,
                maxResults,
                pageToken,
                cancellationToken);
            if (searchResponse == null)
            {
                _logger.LogWarning(
                    "Jira search returned null response for integration {IntegrationName} (ID: {IntegrationId})",
                    integration.Name,
                    integration.Id);
                return (new List<ExternalTicketDto>(), true, null);
            }
            _logger.LogDebug("Fetched {TicketCount} tickets, IsLast={IsLast}",
                searchResponse.Tickets?.Count ?? 0, searchResponse.IsLast);
            var tickets = (await Task.WhenAll(searchResponse.Tickets?.Select(async jiraTicket => 
                await MapJiraTicketToDtoAsync(jiraTicket, integration, cancellationToken)) ?? Array.Empty<Task<ExternalTicketDto>>())).ToList();
            return (tickets, searchResponse.IsLast, searchResponse.NextPageToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Failed to fetch tickets from Jira integration {IntegrationName} (ID: {IntegrationId}): " +
                "JQL={Jql}, StartAt={StartAt}, MaxResults={MaxResults}, NextPageToken={NextPageToken}",
                integration.Name,
                integration.Id,
                jql,
                startAt,
                maxResults,
                pageToken ?? "(none)");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error fetching tickets from Jira integration {IntegrationName} (ID: {IntegrationId}): " +
                "JQL={Jql}, StartAt={StartAt}, MaxResults={MaxResults}, NextPageToken={NextPageToken}",
                integration.Name,
                integration.Id,
                jql,
                startAt,
                maxResults,
                pageToken ?? "(none)");
            throw;
        }
    }

    public async Task<ExternalTicketDto?> GetTicketByIdAsync(
        Integration integration,
        string externalTicketId,
        CancellationToken cancellationToken = default)
    {
        var apiClient = _apiClientFactory.CreateClient(integration);
        var fields = "key,status,priority,summary,description,comment,created,updated";
        
        try
        {
            var jiraTicket = await apiClient.GetIssueAsync(externalTicketId, fields, cancellationToken);
            
            if (jiraTicket == null)
            {
                _logger.LogWarning("Jira ticket {TicketId} not found in integration {IntegrationId}",
                    externalTicketId, integration.Id);
                return null;
            }
            
            return await MapJiraTicketToDtoAsync(jiraTicket, integration, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch ticket {TicketId} from Jira integration {IntegrationId}",
                externalTicketId, integration.Id);
            throw;
        }
    }

    public async Task<CommentDto> AddCommentAsync(
        Integration integration,
        string externalTicketId,
        string content,
        string author,
        CancellationToken cancellationToken = default)
    {
        var apiClient = _apiClientFactory.CreateClient(integration);
        
        // Convert markdown to appropriate format (ADF for Cloud, HTML for On-Premise)
        var commentBody = await _contentConverter.ConvertMarkdownToCommentBodyAsync(
            content, 
            IntegrationTypeDetector.DetectJiraType(integration.Url), 
            cancellationToken);
        
        try
        {
            await apiClient.AddCommentAsync(externalTicketId, commentBody, cancellationToken);
            
            return new CommentDto(
                Guid.NewGuid().ToString(),
                author,
                content
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to add comment to Jira ticket {TicketId}",
                externalTicketId);
            throw;
        }
    }

    public async Task<ExternalTicketCreationResult> CreateIssueAsync(
        Integration integration,
        string summary,
        string description,
        string issueTypeName,
        CancellationToken cancellationToken = default)
    {
        var apiClient = _apiClientFactory.CreateClient(integration);
        
        try
        {
            _logger.LogInformation(
                "Creating JIRA issue in integration {IntegrationId}: Summary='{Summary}', IssueType='{IssueType}'",
                integration.Id,
                summary,
                issueTypeName);
            
            // Step 1: Get project ID from filter query
            var projectId = await GetProjectIdFromFilterQueryAsync(apiClient, integration, cancellationToken);
            
            // Step 2: Resolve issue type name to ID
            var issueTypeId = await GetIssueTypeIdAsync(apiClient, issueTypeName, cancellationToken);
            
            // Step 3: Convert markdown description to appropriate format
            var descriptionBody = await _contentConverter.ConvertMarkdownToDescriptionAsync(
                description,
                IntegrationTypeDetector.DetectJiraType(integration.Url),
                cancellationToken);
            
            // Step 4: Build create issue request
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
            
            _logger.LogDebug(
                "Creating JIRA issue in project {ProjectId} with type {IssueTypeId} for integration {IntegrationId}", 
                projectId, 
                issueTypeId, 
                integration.Id);
            
            var response = await apiClient.CreateIssueAsync(request, cancellationToken);
            
            if (string.IsNullOrEmpty(response.Key))
            {
                _logger.LogError("JIRA returned null or empty issue key for integration {IntegrationId}", integration.Id);
                throw new InvalidOperationException("Failed to create JIRA issue: No issue key returned.");
            }
            
            var baseUrl = integration.Url?.TrimEnd('/') ?? string.Empty;
            var issueUrl = $"{baseUrl}/browse/{response.Key}";
            
            _logger.LogInformation(
                "Successfully created JIRA issue {IssueKey} in integration {IntegrationId}",
                response.Key,
                integration.Id);
            
            return new ExternalTicketCreationResult(
                IssueKey: response.Key,
                IssueUrl: issueUrl,
                IssueId: response.Id
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to create JIRA issue in integration {IntegrationId}", integration.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating JIRA issue in integration {IntegrationId}", integration.Id);
            throw;
        }
    }

    private async Task<string> GetProjectIdFromFilterQueryAsync(
        IJiraApiClient apiClient,
        Integration integration,
        CancellationToken cancellationToken = default)
    {
        // Build JQL query from FilterQuery (or use default if empty)
        var jql = string.IsNullOrEmpty(integration.FilterQuery) 
            ? "ORDER BY updated DESC" 
            : integration.FilterQuery;
        
        _logger.LogDebug(
            "Fetching project ID from JIRA integration {IntegrationId} using JQL: {Jql}", 
            integration.Id, 
            jql);
        
        var searchResponse = await apiClient.SearchIssuesAsync(jql, "project", 0, 1, null, cancellationToken);
        
        var projectId = searchResponse?.Tickets?.FirstOrDefault()?.Fields?.Project?.Id;
        
        if (string.IsNullOrEmpty(projectId))
        {
            _logger.LogError("No project found in FilterQuery results for integration {IntegrationId}", integration.Id);
            throw new InvalidOperationException(
                $"No project found in FilterQuery results for integration {integration.Id}. " +
                $"Ensure FilterQuery returns at least one issue.");
        }
        
        _logger.LogDebug("Resolved project ID {ProjectId} for integration {IntegrationId}", projectId, integration.Id);
        return projectId;
    }

    private async Task<string> GetIssueTypeIdAsync(
        IJiraApiClient apiClient,
        string issueTypeName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Fetching issue type ID for '{IssueTypeName}'", 
            issueTypeName);
        
        var issueTypes = await apiClient.GetIssueTypesAsync(cancellationToken);
        
        var issueType = issueTypes?.FirstOrDefault(it => 
            string.Equals(it.Name, issueTypeName, StringComparison.OrdinalIgnoreCase));
        
        if (issueType == null)
        {
            _logger.LogError("Issue type '{IssueTypeName}' not found", issueTypeName);
            throw new ArgumentException(
                $"Issue type '{issueTypeName}' not found in JIRA. " +
                $"Available types: {string.Join(", ", issueTypes?.Select(it => it.Name) ?? new List<string>())}");
        }
        
        _logger.LogDebug(
            "Resolved issue type '{IssueTypeName}' to ID {IssueTypeId}", 
            issueTypeName, 
            issueType.Id);
        
        return issueType.Id;
    }

    /// <summary>
    /// Maps a Jira ticket to ExternalTicketDto with content conversion.
    /// </summary>
    private async Task<ExternalTicketDto> MapJiraTicketToDtoAsync(
        JiraTicket jiraTicket,
        Integration integration,
        CancellationToken cancellationToken = default)
    {
        var statusName = jiraTicket.Fields?.Status?.Name ?? "Unknown";
        int? statusCategoryId = jiraTicket.Fields?.Status?.StatusCategory?.Id;
        var priorityName = jiraTicket.Fields?.Priority?.Name ?? "Medium";
        var priorityId = jiraTicket.Fields?.Priority?.Id;

        // Set priorityValue based on priorityId if possible, else fallback to name-based
        int priorityValue = 2; // Default to medium
        if (!string.IsNullOrEmpty(priorityId) && int.TryParse(priorityId, out var parsedValue))
        {
            priorityValue = parsedValue;
        }
        else
        {
            priorityValue = MapPriorityToValue(priorityName);
        }

        var comments = await ConvertCommentsAsync(
            jiraTicket.Fields?.Comment?.Comments,
            IntegrationTypeDetector.DetectJiraType(integration.Url),
            cancellationToken);

        // Build external URL directly from integration base URL and ticket key
        var baseUrl = integration.Url?.TrimEnd('/') ?? string.Empty;
        var externalUrl = !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(jiraTicket.Key)
            ? $"{baseUrl}/browse/{jiraTicket.Key}"
            : string.Empty;

        return new ExternalTicketDto(
            IntegrationId: integration.Id,
            ExternalTicketId: jiraTicket.Key ?? "UNKNOWN",
            Title: jiraTicket.Fields?.Summary ?? "Untitled",
            Description: await ExtractDescriptionTextAsync(
                jiraTicket.Fields?.Description,
                IntegrationTypeDetector.DetectJiraType(integration.Url),
                cancellationToken),
            StatusName: statusName,
            StatusColor: GetStatusColor(statusCategoryId, statusName),
            PriorityName: priorityName,
            PriorityColor: GetPriorityColor(priorityId, priorityName),
            PriorityValue: priorityValue,
            ExternalUrl: externalUrl,
            Comments: comments
        );
    }

    /// <summary>
    /// Converts all Jira comments from their native format to Markdown.
    /// </summary>
    private async Task<List<CommentDto>> ConvertCommentsAsync(
        IEnumerable<JiraComment>? jiraComments,
        Domain.Enums.JiraType jiraType,
        CancellationToken cancellationToken = default)
    {
        if (jiraComments == null || !jiraComments.Any())
        {
            return new List<CommentDto>();
        }
        
        try
        {
            var comments = new List<CommentDto>();
            foreach (var comment in jiraComments)
            {
                var markdown = await _contentConverter.ConvertCommentBodyToMarkdownAsync(
                    comment.Body,
                    jiraType,
                    cancellationToken);
                
                comments.Add(new CommentDto(
                    comment.Id ?? Guid.NewGuid().ToString(),
                    comment.Author?.DisplayName ?? "Unknown",
                    markdown ?? string.Empty
                ));
            }
            
            _logger.LogDebug("Successfully converted {Count} comments to Markdown", comments.Count);
            return comments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert {Count} comments to Markdown. Sync operation will fail.", 
                jiraComments.Count());
            throw;
        }
    }

    /// <summary>
    /// Converts Jira ticket description from its native format to Markdown.
    /// </summary>
    private async Task<string> ExtractDescriptionTextAsync(
        JsonElement? description,
        Domain.Enums.JiraType jiraType,
        CancellationToken cancellationToken = default)
    {
        if (!description.HasValue || 
            description.Value.ValueKind == JsonValueKind.Undefined || 
            description.Value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }
        
        try
        {
            var markdown = await _contentConverter.ConvertDescriptionToMarkdownAsync(
                description.Value,
                jiraType,
                cancellationToken);
            
            _logger.LogDebug("Successfully converted description to Markdown: {MarkdownLength} characters", 
                markdown?.Length ?? 0);
            return markdown ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert description to Markdown. Sync operation will fail.");
            throw;
        }
    }

    private int MapPriorityToValue(string priorityName)
    {
        return priorityName.ToLowerInvariant() switch
        {
            "highest" or "critical" or "blocker" => 4,
            "high" => 3,
            "medium" or "normal" => 2,
            "low" or "lowest" or "trivial" => 1,
            _ => 2 // Default to medium
        };
    }

    // ID-based color mappings (up to 10 each)
    private static readonly Dictionary<int, string> StatusCategoryIdColorMap = new()
    {
        // Example: { 2, "bg-emerald-500/20 text-emerald-400" },
        // Add up to 10 statusCategory IDs and their colors here
        { 1, "bg-blue-500/20 text-blue-400" }, // New/To Do
        { 2, "bg-yellow-500/20 text-yellow-400" }, // In Progress
        { 3, "bg-emerald-500/20 text-emerald-400" }, // Done
        { 4, "bg-purple-500/20 text-purple-400" },
        { 5, "bg-red-500/20 text-red-400" },
        { 6, "bg-orange-500/20 text-orange-400" },
        { 7, "bg-slate-500/20 text-slate-400" },
        { 8, "bg-pink-500/20 text-pink-400" },
        { 9, "bg-cyan-500/20 text-cyan-400" },
        { 10, "bg-lime-500/20 text-lime-400" }
    };

    private static readonly Dictionary<string, string> PriorityIdColorMap = new()
    {
        // Example: { "1", "bg-red-500/10 text-red-400 border border-red-500/20" },
        // Add up to 10 priority IDs and their colors here
        { "1", "bg-red-500/10 text-red-400 border border-red-500/20" },
        { "2", "bg-orange-500/10 text-orange-400 border border-orange-500/20" },
        { "3", "bg-blue-500/10 text-blue-400 border border-blue-500/20" },
        { "4", "bg-slate-500/10 text-slate-400 border border-slate-500/20" },
        { "5", "bg-green-500/10 text-green-400 border border-green-500/20" },
        { "6", "bg-pink-500/10 text-pink-400 border border-pink-500/20" },
        { "7", "bg-cyan-500/10 text-cyan-400 border border-cyan-500/20" },
        { "8", "bg-lime-500/10 text-lime-400 border border-lime-500/20" },
        { "9", "bg-yellow-500/10 text-yellow-400 border border-yellow-500/20" },
        { "10", "bg-purple-500/10 text-purple-400 border border-purple-500/20" }
    };

    private string GetStatusColor(int? statusCategoryId, string statusName)
    {
        if (statusCategoryId.HasValue && StatusCategoryIdColorMap.TryGetValue(statusCategoryId.Value, out var colorById))
            return colorById;

        var lowerStatus = statusName.ToLowerInvariant();
        if (lowerStatus.Contains("done") || lowerStatus.Contains("complete") || lowerStatus.Contains("closed"))
            return "bg-emerald-500/20 text-emerald-400";
        if (lowerStatus.Contains("progress") || lowerStatus.Contains("review"))
            return "bg-yellow-500/20 text-yellow-400";
        if (lowerStatus.Contains("todo") || lowerStatus.Contains("to do"))
            return "bg-purple-500/20 text-purple-400";
        return "bg-blue-500/20 text-blue-400"; // Default for new/open
    }

    private string GetPriorityColor(string? priorityId, string priorityName)
    {
        if (!string.IsNullOrEmpty(priorityId) && PriorityIdColorMap.TryGetValue(priorityId, out var colorById))
            return colorById;

        var lowerPriority = priorityName.ToLowerInvariant();
        if (lowerPriority.Contains("highest") || lowerPriority.Contains("critical") || lowerPriority.Contains("blocker"))
            return "bg-red-500/10 text-red-400 border border-red-500/20";
        if (lowerPriority.Contains("high"))
            return "bg-orange-500/10 text-orange-400 border border-orange-500/20";
        if (lowerPriority.Contains("low") || lowerPriority.Contains("lowest") || lowerPriority.Contains("trivial"))
            return "bg-slate-500/10 text-slate-400 border border-slate-500/20";
        return "bg-blue-500/10 text-blue-400 border border-blue-500/20"; // Default for medium
    }
}
