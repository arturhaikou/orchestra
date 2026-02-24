using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub;

public class GitHubTicketProvider : ITicketProvider
{
    private readonly IGitHubApiClientFactory _apiClientFactory;
    private readonly ILogger<GitHubTicketProvider> _logger;

    public GitHubTicketProvider(IGitHubApiClientFactory apiClientFactory, ILogger<GitHubTicketProvider> logger)
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
            
            // GitHub uses page-based pagination (1-indexed)
            var page = string.IsNullOrEmpty(pageToken) ? 1 : int.Parse(pageToken);
            var (issues, hasNextPage) = await client.GetRepositoryIssuesAsync(page, maxResults, cancellationToken);

            var tickets = new List<ExternalTicketDto>();

            foreach (var issue in issues)
            {
                var comments = await client.GetIssueCommentsAsync(issue.Number, cancellationToken);
                var commentDtos = comments.Select(c => new CommentDto(
                    Id: c.Id.ToString(),
                    Author: c.User?.Login ?? "Unknown",
                    Content: c.Body,
                    Timestamp: c.UpdatedAt
                )).ToList();

                var ticket = new ExternalTicketDto(
                    IntegrationId: integration.Id,
                    ExternalTicketId: issue.Number.ToString(),
                    Title: issue.Title,
                    Description: issue.Body,
                    StatusName: issue.State,
                    StatusColor: GetStatusColor(issue.State),
                    PriorityName: GetPriorityFromLabels(issue.Labels),
                    PriorityColor: GetPriorityColor(issue.Labels),
                    PriorityValue: GetPriorityValue(issue.Labels),
                    ExternalUrl: issue.HtmlUrl,
                    Comments: commentDtos
                );

                tickets.Add(ticket);
            }

            // isLast is derived from the Link header (presence of rel="next"), which is
            // authoritative even when the page is exactly full.
            var isLast = !hasNextPage;
            var nextPageToken = isLast ? null : (page + 1).ToString();

            _logger.LogInformation($"Fetched {tickets.Count} tickets from GitHub");
            return (tickets, isLast, nextPageToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tickets from GitHub");
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
            
            if (!int.TryParse(externalTicketId, out var issueNumber))
                return null;

            var issue = await client.GetIssueAsync(issueNumber, cancellationToken);
            
            if (issue == null)
                return null;

            var comments = await client.GetIssueCommentsAsync(issueNumber, cancellationToken);
            var commentDtos = comments.Select(c => new CommentDto(
                Id: c.Id.ToString(),
                Author: c.User?.Login ?? "Unknown",
                Content: c.Body,
                Timestamp: c.UpdatedAt
            )).ToList();

            var ticket = new ExternalTicketDto(
                IntegrationId: integration.Id,
                ExternalTicketId: issue.Number.ToString(),
                Title: issue.Title,
                Description: issue.Body,
                StatusName: issue.State,
                StatusColor: GetStatusColor(issue.State),
                PriorityName: GetPriorityFromLabels(issue.Labels),
                PriorityColor: GetPriorityColor(issue.Labels),
                PriorityValue: GetPriorityValue(issue.Labels),
                ExternalUrl: issue.HtmlUrl,
                Comments: commentDtos
            );

            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error fetching ticket {externalTicketId} from GitHub");
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
            
            if (!int.TryParse(externalTicketId, out var issueNumber))
                throw new InvalidOperationException($"Invalid issue number: {externalTicketId}");

            var comment = await client.AddCommentAsync(issueNumber, content, cancellationToken);

            return new CommentDto(
                Id: comment.Id.ToString(),
                Author: comment.User?.Login ?? author,
                Content: comment.Body,
                Timestamp: comment.CreatedAt
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error adding comment to GitHub issue {externalTicketId}");
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
            {
                labels.Add(issueTypeName.ToLower());
            }

            var issue = await client.CreateIssueAsync(summary, description, labels, cancellationToken);

            return new ExternalTicketCreationResult(
                IssueKey: $"#{issue.Number}",
                IssueUrl: issue.HtmlUrl,
                IssueId: issue.Number.ToString()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating GitHub issue");
            throw;
        }
    }

    private string GetStatusColor(string state)
    {
        return state.ToLower() switch
        {
            "open" => "bg-blue-100 text-blue-800",
            "closed" => "bg-red-100 text-red-800",
            _ => "bg-gray-100 text-gray-800"
        };
    }

    private string GetPriorityFromLabels(List<Orchestra.Infrastructure.Integrations.Providers.GitHub.Models.GitHubLabel> labels)
    {
        var priorityLabel = labels.FirstOrDefault(l => 
            l.Name.Contains("priority", StringComparison.OrdinalIgnoreCase) ||
            l.Name.Contains("urgent", StringComparison.OrdinalIgnoreCase) ||
            l.Name.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
            l.Name.Contains("high", StringComparison.OrdinalIgnoreCase) ||
            l.Name.Contains("low", StringComparison.OrdinalIgnoreCase));

        return priorityLabel?.Name ?? "Medium";
    }

    private string GetPriorityColor(List<Orchestra.Infrastructure.Integrations.Providers.GitHub.Models.GitHubLabel> labels)
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

    private int GetPriorityValue(List<Orchestra.Infrastructure.Integrations.Providers.GitHub.Models.GitHubLabel> labels)
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
