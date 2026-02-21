using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab;

public class GitLabTicketProvider : ITicketProvider
{
    private readonly IGitLabApiClientFactory _apiClientFactory;
    private readonly ILogger<GitLabTicketProvider> _logger;

    public GitLabTicketProvider(IGitLabApiClientFactory apiClientFactory, ILogger<GitLabTicketProvider> logger)
    {
        _apiClientFactory = apiClientFactory;
        _logger = logger;
    }

    public async Task<(List<ExternalTicketDto> Tickets, bool IsLast, string? NextPageToken)> FetchTicketsAsync(
        Integration integration,
        int startAt = 0,
        int maxResults = 50,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _apiClientFactory.CreateClient(integration);

            // GitLab uses page-based pagination (1-indexed)
            var page = string.IsNullOrEmpty(pageToken) ? 1 : int.Parse(pageToken);
            var issues = await client.GetProjectIssuesAsync(page, maxResults, cancellationToken);

            var tickets = new List<ExternalTicketDto>();

            foreach (var issue in issues)
            {
                var notes = await client.GetIssueNotesAsync(issue.Iid, cancellationToken);
                var commentDtos = notes.Select(n => new CommentDto(
                    Id: n.Id.ToString(),
                    Author: n.Author?.Username ?? "Unknown",
                    Content: n.Body,
                    Timestamp: n.UpdatedAt
                )).ToList();

                tickets.Add(new ExternalTicketDto(
                    IntegrationId: integration.Id,
                    ExternalTicketId: issue.Iid.ToString(),
                    Title: issue.Title,
                    Description: issue.Description,
                    StatusName: issue.State,
                    StatusColor: GetStatusColor(issue.State),
                    PriorityName: GetPriorityFromLabels(issue.Labels),
                    PriorityColor: GetPriorityColor(issue.Labels),
                    PriorityValue: GetPriorityValue(issue.Labels),
                    ExternalUrl: issue.WebUrl,
                    Comments: commentDtos
                ));
            }

            var isLast = issues.Count < maxResults;
            var nextPageToken = isLast ? null : (page + 1).ToString();

            _logger.LogInformation("Fetched {Count} tickets from GitLab", tickets.Count);
            return (tickets, isLast, nextPageToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tickets from GitLab");
            throw;
        }
    }

    public async Task<ExternalTicketDto?> GetTicketByIdAsync(
        Integration integration,
        string externalTicketId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _apiClientFactory.CreateClient(integration);

            if (!int.TryParse(externalTicketId, out var iid))
                return null;

            var issue = await client.GetIssueAsync(iid, cancellationToken);

            if (issue == null)
                return null;

            var notes = await client.GetIssueNotesAsync(iid, cancellationToken);
            var commentDtos = notes.Select(n => new CommentDto(
                Id: n.Id.ToString(),
                Author: n.Author?.Username ?? "Unknown",
                Content: n.Body,
                Timestamp: n.UpdatedAt
            )).ToList();

            return new ExternalTicketDto(
                IntegrationId: integration.Id,
                ExternalTicketId: issue.Iid.ToString(),
                Title: issue.Title,
                Description: issue.Description,
                StatusName: issue.State,
                StatusColor: GetStatusColor(issue.State),
                PriorityName: GetPriorityFromLabels(issue.Labels),
                PriorityColor: GetPriorityColor(issue.Labels),
                PriorityValue: GetPriorityValue(issue.Labels),
                ExternalUrl: issue.WebUrl,
                Comments: commentDtos
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching ticket {TicketId} from GitLab", externalTicketId);
            return null;
        }
    }

    public async Task<CommentDto> AddCommentAsync(
        Integration integration,
        string externalTicketId,
        string content,
        string author,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _apiClientFactory.CreateClient(integration);

            if (!int.TryParse(externalTicketId, out var iid))
                throw new InvalidOperationException($"Invalid GitLab issue number: {externalTicketId}");

            var note = await client.AddNoteAsync(iid, content, cancellationToken);

            return new CommentDto(
                Id: note.Id.ToString(),
                Author: note.Author?.Username ?? author,
                Content: note.Body,
                Timestamp: note.CreatedAt
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to GitLab issue {TicketId}", externalTicketId);
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
        try
        {
            var client = _apiClientFactory.CreateClient(integration);

            var labels = new List<string>();
            if (!string.IsNullOrEmpty(issueTypeName) && issueTypeName.ToLower() != "task")
                labels.Add(issueTypeName.ToLower());

            var issue = await client.CreateIssueAsync(summary, description, labels, cancellationToken);

            return new ExternalTicketCreationResult(
                IssueKey: $"#{issue.Iid}",
                IssueUrl: issue.WebUrl,
                IssueId: issue.Iid.ToString()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GitLab issue");
            throw;
        }
    }

    private static string GetStatusColor(string state) =>
        state.ToLower() switch
        {
            "opened" => "bg-blue-100 text-blue-800",
            "closed" => "bg-red-100 text-red-800",
            _ => "bg-gray-100 text-gray-800"
        };

    private static string GetPriorityFromLabels(List<string> labels)
    {
        var priorityLabel = labels.FirstOrDefault(l =>
            l.Contains("priority", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("urgent", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("high", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("low", StringComparison.OrdinalIgnoreCase));

        return priorityLabel ?? "Medium";
    }

    private static string GetPriorityColor(List<string> labels)
    {
        var priorityName = GetPriorityFromLabels(labels).ToLower();

        return priorityName switch
        {
            var p when p.Contains("critical") || p.Contains("urgent") => "bg-red-100 text-red-800",
            var p when p.Contains("high") => "bg-orange-100 text-orange-800",
            var p when p.Contains("low") => "bg-green-100 text-green-800",
            _ => "bg-yellow-100 text-yellow-800"
        };
    }

    private static int GetPriorityValue(List<string> labels)
    {
        var priorityName = GetPriorityFromLabels(labels).ToLower();

        return priorityName switch
        {
            var p when p.Contains("critical") || p.Contains("urgent") => 1,
            var p when p.Contains("high") => 2,
            var p when p.Contains("medium") => 3,
            _ => 4
        };
    }
}
