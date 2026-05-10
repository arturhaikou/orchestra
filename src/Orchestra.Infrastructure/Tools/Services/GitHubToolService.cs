using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Orchestra.Application.CodeReview;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;

namespace Orchestra.Infrastructure.Tools.Services;

public class GitHubToolService : IGitHubToolService
{
    private readonly IGitHubApiClientFactory _apiClientFactory;
    private readonly IIntegrationResolver _integrationResolver;
    private readonly ICodeReviewPipeline _codeReviewPipeline;
    private readonly ILogger<GitHubToolService> _logger;

    public GitHubToolService(
        IGitHubApiClientFactory apiClientFactory,
        IIntegrationResolver integrationResolver,
        ICodeReviewPipeline codeReviewPipeline,
        ILogger<GitHubToolService> logger)
    {
        _apiClientFactory = apiClientFactory;
        _integrationResolver = integrationResolver;
        _codeReviewPipeline = codeReviewPipeline;
        _logger = logger;
    }

    public async Task<GitHubIssueResult> GetIssueAsync(string workspaceId, string integrationId, string issueNumber)
    {
        try
        {
            _logger.LogInformation("GitHub get_issue: workspaceId={WorkspaceId} issueNumber={IssueNumber}", workspaceId, issueNumber);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitHubIssueResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (!int.TryParse(issueNumber, out var issueNum))
                return new GitHubIssueResult { Success = false, Error = $"Invalid issue number: {issueNumber}" };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITHUB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var issue = await apiClient.GetIssueAsync(issueNum);

            return new GitHubIssueResult
            {
                Success = true,
                Number = issue.Number,
                Title = issue.Title,
                Body = issue.Body,
                State = issue.State,
                Url = issue.HtmlUrl,
                Assignees = issue.Assignee != null
                    ? new[] { issue.Assignee.Login }
                    : Array.Empty<string>(),
                Labels = issue.Labels.Select(l => l.Name).ToArray()
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitHubIssueResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitHub integration"))
        {
            _logger.LogWarning(ex, "No GitHub integration for workspace {WorkspaceId}", workspaceId);
            return new GitHubIssueResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("authenticate"))
        {
            _logger.LogWarning(ex, "GitHub auth failure for workspace {WorkspaceId}", workspaceId);
            return new GitHubIssueResult { Success = false, Error = "Failed to authenticate with GitHub. Please verify the API key." };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "GitHub rate limit for workspace {WorkspaceId}", workspaceId);
            return new GitHubIssueResult { Success = false, Error = "GitHub API rate limit exceeded. Please try again later." };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("permissions"))
        {
            _logger.LogWarning(ex, "GitHub resource not found for workspace {WorkspaceId}", workspaceId);
            return new GitHubIssueResult { Success = false, Error = "GitHub repository not found or insufficient permissions." };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, "GitHub network error for workspace {WorkspaceId}", workspaceId);
            return new GitHubIssueResult { Success = false, Error = "Unable to reach GitHub API. Please check connectivity." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetIssueAsync for workspace {WorkspaceId}", workspaceId);
            return new GitHubIssueResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<GitHubPullRequestResult> GetPullRequestAsync(string workspaceId, string integrationId, string pullNumber)
    {
        try
        {
            _logger.LogInformation("GitHub get_pr: workspaceId={WorkspaceId} pullNumber={PullNumber}", workspaceId, pullNumber);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitHubPullRequestResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (!int.TryParse(pullNumber, out var pullNum))
                return new GitHubPullRequestResult { Success = false, Error = $"Invalid pull request number: {pullNumber}" };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITHUB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var pr = await apiClient.GetPullRequestAsync(pullNum);

            return new GitHubPullRequestResult
            {
                Success = true,
                Number = pr.Number,
                Title = pr.Title,
                Body = pr.Body,
                State = pr.State,
                Merged = pr.Merged,
                Url = pr.HtmlUrl,
                HeadBranch = pr.Head?.Ref ?? string.Empty,
                BaseBranch = pr.Base?.Ref ?? string.Empty,
                Mergeable = pr.Mergeable
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitHubPullRequestResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitHub integration"))
        {
            _logger.LogWarning(ex, "No GitHub integration for workspace {WorkspaceId}", workspaceId);
            return new GitHubPullRequestResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("authenticate"))
        {
            _logger.LogWarning(ex, "GitHub auth failure for workspace {WorkspaceId}", workspaceId);
            return new GitHubPullRequestResult { Success = false, Error = "Failed to authenticate with GitHub. Please verify the API key." };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "GitHub rate limit for workspace {WorkspaceId}", workspaceId);
            return new GitHubPullRequestResult { Success = false, Error = "GitHub API rate limit exceeded. Please try again later." };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("permissions"))
        {
            _logger.LogWarning(ex, "GitHub resource not found for workspace {WorkspaceId}", workspaceId);
            return new GitHubPullRequestResult { Success = false, Error = "GitHub repository not found or insufficient permissions." };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, "GitHub network error for workspace {WorkspaceId}", workspaceId);
            return new GitHubPullRequestResult { Success = false, Error = "Unable to reach GitHub API. Please check connectivity." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetPullRequestAsync for workspace {WorkspaceId}", workspaceId);
            return new GitHubPullRequestResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<GitHubSearchIssuesResult> SearchIssuesAsync(string workspaceId, string integrationId, string query, int? limit = 10)
    {
        try
        {
            _logger.LogInformation("GitHub search_issues: workspaceId={WorkspaceId} query={Query}", workspaceId, query);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitHubSearchIssuesResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (string.IsNullOrWhiteSpace(query))
                return new GitHubSearchIssuesResult { Success = false, Error = "Query cannot be empty." };

            var clampedLimit = Math.Min(limit ?? 10, 30);

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITHUB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var result = await apiClient.SearchIssuesAsync(query, clampedLimit);

            return new GitHubSearchIssuesResult
            {
                Success = true,
                TotalCount = result.TotalCount,
                Items = result.Items.Select(i => new
                {
                    number = i.Number,
                    title = i.Title,
                    state = i.State,
                    url = i.HtmlUrl
                }).ToArray()
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitHubSearchIssuesResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitHub integration"))
        {
            _logger.LogWarning(ex, "No GitHub integration for workspace {WorkspaceId}", workspaceId);
            return new GitHubSearchIssuesResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("authenticate"))
        {
            _logger.LogWarning(ex, "GitHub auth failure for workspace {WorkspaceId}", workspaceId);
            return new GitHubSearchIssuesResult { Success = false, Error = "Failed to authenticate with GitHub. Please verify the API key." };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "GitHub rate limit for workspace {WorkspaceId}", workspaceId);
            return new GitHubSearchIssuesResult { Success = false, Error = "GitHub API rate limit exceeded. Please try again later." };
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            _logger.LogError(ex, "GitHub network error for workspace {WorkspaceId}", workspaceId);
            return new GitHubSearchIssuesResult { Success = false, Error = "Unable to reach GitHub API. Please check connectivity." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SearchIssuesAsync for workspace {WorkspaceId}", workspaceId);
            return new GitHubSearchIssuesResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Creates a new GitHub issue in the workspace's connected repository.
    /// </summary>
    /// <param name="workspaceId">The workspace ID (GUID).</param>
    /// <param name="title">The issue title.</param>
    /// <param name="body">The issue body/description.</param>
    /// <returns>A result object indicating success or failure with details.</returns>
    public async Task<GitHubCreateIssueResult> CreateIssueAsync(string workspaceId, string integrationId, string title, string body)
    {
        try
        {
            _logger.LogInformation("GitHub create_issue: workspaceId={WorkspaceId} title={Title}", workspaceId, title);

            // Input validation
            if (string.IsNullOrWhiteSpace(workspaceId))
                return new GitHubCreateIssueResult { Success = false, Error = "Workspace ID is required." };

            if (string.IsNullOrWhiteSpace(title))
                return new GitHubCreateIssueResult { Success = false, Error = "Title is required." };

            if (string.IsNullOrWhiteSpace(body))
                return new GitHubCreateIssueResult { Success = false, Error = "Body is required." };

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitHubCreateIssueResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            // Integration lookup
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITHUB);

            // API client creation
            var apiClient = _apiClientFactory.CreateClient(integration);

            // API call
            var issue = await apiClient.CreateIssueAsync(title, body);

            _logger.LogInformation("Successfully created GitHub issue #{IssueNumber} for workspace {WorkspaceId}", issue.Number, workspaceId);

            return new GitHubCreateIssueResult
            {
                Success = true,
                Number = issue.Number,
                Url = issue.HtmlUrl,
                Title = issue.Title
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitHubCreateIssueResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitHub integration"))
        {
            _logger.LogWarning(ex, "No GitHub integration for workspace {WorkspaceId}", workspaceId);
            return new GitHubCreateIssueResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitHub API error creating issue for workspace {WorkspaceId}", workspaceId);

            // Map status codes to user-friendly messages
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return new GitHubCreateIssueResult
                {
                    Success = false,
                    Error = "Failed to authenticate with GitHub. Please verify the API key."
                };
            }

            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new GitHubCreateIssueResult
                {
                    Success = false,
                    Error = "GitHub repository not found or insufficient permissions."
                };
            }

            if ((int)ex.StatusCode == 429) // Rate limit
            {
                return new GitHubCreateIssueResult
                {
                    Success = false,
                    Error = "GitHub API rate limit exceeded. Please try again later."
                };
            }

            return new GitHubCreateIssueResult
            {
                Success = false,
                Error = "An error occurred while creating the issue. Please try again."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating GitHub issue for workspace {WorkspaceId}", workspaceId);
            return new GitHubCreateIssueResult
            {
                Success = false,
                Error = "An unexpected error occurred. Please try again."
            };
        }
    }

    /// <summary>
    /// Updates the title and/or body of an existing GitHub issue.
    /// </summary>
    /// <param name="workspaceId">The workspace ID (GUID).</param>
    /// <param name="issueNumber">The GitHub issue number.</param>
    /// <param name="title">New title (optional; if null or empty, omit from patch).</param>
    /// <param name="body">New body (optional; if null or empty, omit from patch).</param>
    /// <returns>A result object indicating success or failure with details.</returns>
    public async Task<GitHubUpdateIssueResult> UpdateIssueAsync(string workspaceId, string integrationId, string issueNumber, string? title, string? body)
    {
        try
        {
            _logger.LogInformation("GitHub update_issue: workspaceId={WorkspaceId} issueNumber={IssueNumber}", workspaceId, issueNumber);

            // Input validation
            if (string.IsNullOrWhiteSpace(workspaceId))
                return new GitHubUpdateIssueResult { Success = false, Error = "Workspace ID is required." };

            if (string.IsNullOrWhiteSpace(issueNumber))
                return new GitHubUpdateIssueResult { Success = false, Error = "Issue number is required." };

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(body))
                return new GitHubUpdateIssueResult { Success = false, Error = "At least one of title or body must be provided." };

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitHubUpdateIssueResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (!int.TryParse(issueNumber, out var issueNum))
                return new GitHubUpdateIssueResult { Success = false, Error = $"Invalid issue number format: {issueNumber}" };

            // Integration lookup
            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITHUB);

            // API client creation
            var apiClient = _apiClientFactory.CreateClient(integration);

            // API call
            var issue = await apiClient.UpdateIssueAsync(issueNum, title, body);

            _logger.LogInformation("Successfully updated GitHub issue #{IssueNumber} for workspace {WorkspaceId}", issue.Number, workspaceId);

            return new GitHubUpdateIssueResult
            {
                Success = true,
                Number = issue.Number,
                Url = issue.HtmlUrl,
                Title = issue.Title
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitHubUpdateIssueResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitHub integration"))
        {
            _logger.LogWarning(ex, "No GitHub integration for workspace {WorkspaceId}", workspaceId);
            return new GitHubUpdateIssueResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitHub API error updating issue for workspace {WorkspaceId}", workspaceId);

            // Map status codes to user-friendly messages
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return new GitHubUpdateIssueResult
                {
                    Success = false,
                    Error = "Failed to authenticate with GitHub. Please verify the API key."
                };
            }

            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new GitHubUpdateIssueResult
                {
                    Success = false,
                    Error = "GitHub repository not found or insufficient permissions."
                };
            }

            if ((int)ex.StatusCode == 429) // Rate limit
            {
                return new GitHubUpdateIssueResult
                {
                    Success = false,
                    Error = "GitHub API rate limit exceeded. Please try again later."
                };
            }

            return new GitHubUpdateIssueResult
            {
                Success = false,
                Error = "An error occurred while updating the issue. Please try again."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating GitHub issue for workspace {WorkspaceId}", workspaceId);
            return new GitHubUpdateIssueResult
            {
                Success = false,
                Error = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task<ReviewToolResult> ReviewPullRequestAsync(
        string workspaceId,
        string integrationId,
        string pullNumber,
        string? modelIdentifier = null,
        string? projectPrinciples = null)
    {
        _logger.LogInformation(
            "GitHub review_pull_request: workspaceId={WorkspaceId} pullNumber={PullNumber}",
            workspaceId, pullNumber);

        if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            return new ReviewToolResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

        if (!int.TryParse(pullNumber, out _))
            return new ReviewToolResult { Success = false, Error = $"Invalid pull request number: {pullNumber}" };

        try
        {
            return await _codeReviewPipeline.ExecuteAsync(
                new ReviewRequest
                {
                    WorkspaceId = workspaceGuid,
                    IntegrationId = integrationId,
                    PrOrMrNumber = pullNumber,
                    ProviderType = ProviderType.GITHUB,
                    ModelIdentifier = modelIdentifier,
                    ProjectPrinciples = projectPrinciples,
                });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new ReviewToolResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitHub integration"))
        {
            _logger.LogWarning(ex, "No GitHub integration for workspace {WorkspaceId}", workspaceId);
            return new ReviewToolResult { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ReviewPullRequestAsync for workspace {WorkspaceId}", workspaceId);
            return new ReviewToolResult { Success = false, Error = ex.Message };
        }
    }

}