using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;

namespace Orchestra.Infrastructure.Tools.Services;

public class GitLabToolService : IGitLabToolService
{
    private readonly IGitLabApiClientFactory _apiClientFactory;
    private readonly IIntegrationResolver _integrationResolver;
    private readonly ILogger<GitLabToolService> _logger;

    public GitLabToolService(
        IGitLabApiClientFactory apiClientFactory,
        IIntegrationResolver integrationResolver,
        ILogger<GitLabToolService> logger)
    {
        _apiClientFactory = apiClientFactory;
        _integrationResolver = integrationResolver;
        _logger = logger;
    }

    public async Task<GitLabIssueResult> GetIssueAsync(string workspaceId, string integrationId, string issueIid)
    {
        try
        {
            _logger.LogInformation("GitLab get_issue: workspaceId={WorkspaceId} issueIid={IssueIid}", workspaceId, issueIid);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitLabIssueResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (!int.TryParse(issueIid, out var issueNum))
                return new GitLabIssueResult { Success = false, Error = $"Invalid issue IID: {issueIid}" };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITLAB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var issue = await apiClient.GetIssueAsync(issueNum);

            if (issue == null)
                return new GitLabIssueResult { Success = false, Error = $"Issue {issueIid} not found in GitLab." };

            return new GitLabIssueResult
            {
                Success = true,
                Iid = issue.Iid,
                Title = issue.Title,
                Description = issue.Description,
                State = issue.State,
                Url = issue.WebUrl,
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitLabIssueResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new GitLabIssueResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitLab API error in GetIssueAsync for workspace {WorkspaceId}", workspaceId);
            return new GitLabIssueResult { Success = false, Error = "Unable to reach GitLab API. Please check connectivity." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetIssueAsync for workspace {WorkspaceId}", workspaceId);
            return new GitLabIssueResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<GitLabMergeRequestResult> GetMergeRequestAsync(string workspaceId, string integrationId, string mrIid)
    {
        try
        {
            _logger.LogInformation("GitLab get_mr: workspaceId={WorkspaceId} mrIid={MrIid}", workspaceId, mrIid);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitLabMergeRequestResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (!int.TryParse(mrIid, out var mrNum))
                return new GitLabMergeRequestResult { Success = false, Error = $"Invalid merge request IID: {mrIid}" };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITLAB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var mergeRequest = await apiClient.GetMergeRequestAsync(mrNum);

            if (mergeRequest == null)
                return new GitLabMergeRequestResult { Success = false, Error = $"Merge request {mrIid} not found in GitLab." };

            return new GitLabMergeRequestResult
            {
                Success = true,
                Iid = mergeRequest.Iid,
                Title = mergeRequest.Title,
                Description = mergeRequest.Description,
                State = mergeRequest.State,
                Merged = mergeRequest.Merged,
                Url = mergeRequest.WebUrl,
                SourceBranch = mergeRequest.SourceBranch,
                TargetBranch = mergeRequest.TargetBranch
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitLabMergeRequestResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new GitLabMergeRequestResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitLab API error in GetMergeRequestAsync for workspace {WorkspaceId}", workspaceId);
            return new GitLabMergeRequestResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<GitLabSearchIssuesResult> SearchIssuesAsync(string workspaceId, string integrationId, string query, int? limit = 10)
    {
        try
        {
            _logger.LogInformation("GitLab search_issues: workspaceId={WorkspaceId} query={Query}", workspaceId, query);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitLabSearchIssuesResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (string.IsNullOrWhiteSpace(query))
                return new GitLabSearchIssuesResult { Success = false, Error = "Search query cannot be empty." };

            var searchLimit = limit ?? 10;
            if (searchLimit > 30) searchLimit = 30;
            if (searchLimit < 1) searchLimit = 1;

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITLAB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var issues = await apiClient.SearchIssuesAsync(query, searchLimit);

            return new GitLabSearchIssuesResult
            {
                Success = true,
                TotalCount = issues.Count,
                Items = issues.Cast<object>().ToArray()
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitLabSearchIssuesResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new GitLabSearchIssuesResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitLab API error in SearchIssuesAsync for workspace {WorkspaceId}", workspaceId);
            return new GitLabSearchIssuesResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<GitLabCreateIssueResult> CreateIssueAsync(string workspaceId, string integrationId, string title, string description)
    {
        try
        {
            _logger.LogInformation("GitLab create_issue: workspaceId={WorkspaceId} title={Title}", workspaceId, title);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitLabCreateIssueResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (string.IsNullOrWhiteSpace(title))
                return new GitLabCreateIssueResult { Success = false, Error = "Issue title cannot be empty." };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITLAB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var createdIssue = await apiClient.CreateIssueAsync(title, description);

            return new GitLabCreateIssueResult
            {
                Success = true,
                Iid = createdIssue.Iid,
                Url = createdIssue.WebUrl,
                Title = createdIssue.Title
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitLabCreateIssueResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new GitLabCreateIssueResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitLab API error in CreateIssueAsync for workspace {WorkspaceId}", workspaceId);
            return new GitLabCreateIssueResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }

    public async Task<GitLabUpdateIssueResult> UpdateIssueAsync(string workspaceId, string integrationId, string issueIid, string? title, string? description)
    {
        try
        {
            _logger.LogInformation("GitLab update_issue: workspaceId={WorkspaceId} issueIid={IssueIid}", workspaceId, issueIid);

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitLabUpdateIssueResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (!int.TryParse(issueIid, out var issueNum))
                return new GitLabUpdateIssueResult { Success = false, Error = $"Invalid issue IID: {issueIid}" };

            if (string.IsNullOrWhiteSpace(title) && description == null)
                return new GitLabUpdateIssueResult { Success = false, Error = "At least one of title or description must be provided." };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITLAB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var updatedIssue = await apiClient.UpdateIssueAsync(issueNum, title, description);

            return new GitLabUpdateIssueResult
            {
                Success = true,
                Iid = updatedIssue.Iid,
                Url = updatedIssue.WebUrl,
                Title = updatedIssue.Title
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitLabUpdateIssueResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new GitLabUpdateIssueResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitLab API error in UpdateIssueAsync for workspace {WorkspaceId}", workspaceId);
            return new GitLabUpdateIssueResult { Success = false, Error = $"Unexpected error: {ex.Message}" };
        }
    }
}
