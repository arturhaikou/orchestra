using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.CodeReview;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;

namespace Orchestra.Infrastructure.Tools.Services;

public class GitLabToolService : IGitLabToolService
{
    private readonly IGitLabApiClientFactory _apiClientFactory;
    private readonly IIntegrationResolver _integrationResolver;
    private readonly ICodeReviewPipeline _codeReviewPipeline;
    private readonly IAiCliIntegrationDataAccess _cliIntegrationDataAccess;
    private readonly ILogger<GitLabToolService> _logger;

    public GitLabToolService(
        IGitLabApiClientFactory apiClientFactory,
        IIntegrationResolver integrationResolver,
        ICodeReviewPipeline codeReviewPipeline,
        IAiCliIntegrationDataAccess cliIntegrationDataAccess,
        ILogger<GitLabToolService> logger)
    {
        _apiClientFactory = apiClientFactory;
        _integrationResolver = integrationResolver;
        _codeReviewPipeline = codeReviewPipeline;
        _cliIntegrationDataAccess = cliIntegrationDataAccess;
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

    public async Task<GitLabPushBranchResult> PushBranchAsync(
        string workspaceId,
        string integrationId,
        string cliIntegrationId,
        string branchName)
    {
        try
        {
            _logger.LogInformation("GitLab push_branch: workspaceId={WorkspaceId} branch={Branch}", workspaceId, branchName);

            if (string.IsNullOrWhiteSpace(workspaceId))
                return new GitLabPushBranchResult { Success = false, Error = "Workspace ID is required." };

            if (string.IsNullOrWhiteSpace(cliIntegrationId))
                return new GitLabPushBranchResult { Success = false, Error = "CLI integration ID is required." };

            if (string.IsNullOrWhiteSpace(branchName))
                return new GitLabPushBranchResult { Success = false, Error = "Branch name is required." };

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitLabPushBranchResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            if (!Guid.TryParse(cliIntegrationId, out var cliGuid))
                return new GitLabPushBranchResult { Success = false, Error = $"Invalid CLI integration ID format: {cliIntegrationId}" };

            var cliIntegration = await _cliIntegrationDataAccess.GetByIdAsync(cliGuid);
            if (cliIntegration == null)
                return new GitLabPushBranchResult { Success = false, Error = $"CLI integration {cliIntegrationId} not found." };

            if (cliIntegration.WorkspaceId != workspaceGuid)
                return new GitLabPushBranchResult { Success = false, Error = "CLI integration does not belong to this workspace." };

            var repositoryPath = cliIntegration.WorkingDirectory;

            if (!Directory.Exists(repositoryPath) || !Directory.Exists(Path.Combine(repositoryPath, ".git")))
                return new GitLabPushBranchResult { Success = false, Error = $"CLI integration working directory '{repositoryPath}' is not a local git repository." };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITLAB);
            var remoteUrl = _apiClientFactory.GetAuthenticatedRemoteUrl(integration);

            var result = await RunGitPushAsync(repositoryPath, remoteUrl, branchName);

            if (!result.Success)
            {
                _logger.LogWarning("git push failed for workspace {WorkspaceId}: {Error}", workspaceId, result.Error);
                return new GitLabPushBranchResult { Success = false, Branch = branchName, Error = result.Error };
            }

            _logger.LogInformation("Successfully pushed branch {Branch} for workspace {WorkspaceId}", branchName, workspaceId);
            return new GitLabPushBranchResult { Success = true, Branch = branchName, Output = result.Output };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitLabPushBranchResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new GitLabPushBranchResult { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error pushing branch for workspace {WorkspaceId}", workspaceId);
            return new GitLabPushBranchResult { Success = false, Error = "An unexpected error occurred. Please try again." };
        }
    }

    private static async Task<(bool Success, string? Output, string? Error)> RunGitPushAsync(
        string repositoryPath,
        string remoteUrl,
        string branchName)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"push {remoteUrl} {branchName}:{branchName}",
                WorkingDirectory = repositoryPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = RedactCredentials(string.IsNullOrWhiteSpace(stdout) ? stderr.Trim() : stdout.Trim());

        if (process.ExitCode != 0)
            return (false, null, string.IsNullOrWhiteSpace(stderr) ? $"git push exited with code {process.ExitCode}" : RedactCredentials(stderr.Trim()));

        return (true, output, null);
    }

    private static string RedactCredentials(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        return Regex.Replace(text, @"https://[^\s/@]+:[^\s/@]+@", "https://***:***@", RegexOptions.IgnoreCase);
    }

    public async Task<GitLabCreateMergeRequestResult> CreateMergeRequestAsync(
        string workspaceId,
        string integrationId,
        string title,
        string description,
        string sourceBranch,
        string targetBranch)
    {
        try
        {
            _logger.LogInformation("GitLab create_mr: workspaceId={WorkspaceId} source={SourceBranch} target={TargetBranch}", workspaceId, sourceBranch, targetBranch);

            if (string.IsNullOrWhiteSpace(workspaceId))
                return new GitLabCreateMergeRequestResult { Success = false, Error = "Workspace ID is required." };

            if (string.IsNullOrWhiteSpace(title))
                return new GitLabCreateMergeRequestResult { Success = false, Error = "Title is required." };

            if (string.IsNullOrWhiteSpace(sourceBranch))
                return new GitLabCreateMergeRequestResult { Success = false, Error = "Source branch is required." };

            if (string.IsNullOrWhiteSpace(targetBranch))
                return new GitLabCreateMergeRequestResult { Success = false, Error = "Target branch is required." };

            if (!Guid.TryParse(workspaceId, out var workspaceGuid))
                return new GitLabCreateMergeRequestResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

            var integration = await _integrationResolver.ResolveAsync(workspaceGuid, integrationId, ProviderType.GITLAB);
            var apiClient = _apiClientFactory.CreateClient(integration);
            var mr = await apiClient.CreateMergeRequestAsync(title, description, sourceBranch, targetBranch);

            _logger.LogInformation("Successfully created GitLab merge request !{Iid} for workspace {WorkspaceId}", mr.Iid, workspaceId);

            return new GitLabCreateMergeRequestResult
            {
                Success = true,
                Iid = mr.Iid,
                Url = mr.WebUrl,
                Title = mr.Title,
                SourceBranch = mr.SourceBranch,
                TargetBranch = mr.TargetBranch,
            };
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("integrationId is required") ||
            ex.Message.Contains("No active integration found for the supplied ID"))
        {
            _logger.LogWarning(ex, "Integration resolution failed for workspace {WorkspaceId}", workspaceId);
            return new GitLabCreateMergeRequestResult { Success = false, Error = ex.Message };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new GitLabCreateMergeRequestResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GitLab API error creating merge request for workspace {WorkspaceId}", workspaceId);

            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return new GitLabCreateMergeRequestResult { Success = false, Error = "Failed to authenticate with GitLab. Please verify the API token." };

            if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new GitLabCreateMergeRequestResult { Success = false, Error = "GitLab project not found or insufficient permissions." };

            return new GitLabCreateMergeRequestResult { Success = false, Error = "An error occurred while creating the merge request. Please try again." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating GitLab merge request for workspace {WorkspaceId}", workspaceId);
            return new GitLabCreateMergeRequestResult { Success = false, Error = "An unexpected error occurred. Please try again." };
        }
    }

    public async Task<ReviewToolResult> ReviewMergeRequestAsync(
        string workspaceId,
        string integrationId,
        string mrIid,
        string? modelIdentifier = null,
        string? projectPrinciples = null)
    {
        _logger.LogInformation(
            "GitLab review_merge_request: workspaceId={WorkspaceId} mrIid={MrIid}",
            workspaceId, mrIid);

        if (!Guid.TryParse(workspaceId, out var workspaceGuid))
            return new ReviewToolResult { Success = false, Error = $"Invalid workspace ID format: {workspaceId}" };

        if (!int.TryParse(mrIid, out _))
            return new ReviewToolResult { Success = false, Error = $"Invalid merge request IID: {mrIid}" };

        try
        {
            return await _codeReviewPipeline.ExecuteAsync(
                new ReviewRequest
                {
                    WorkspaceId = workspaceGuid,
                    IntegrationId = integrationId,
                    PrOrMrNumber = mrIid,
                    ProviderType = ProviderType.GITLAB,
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("No active GitLab integration"))
        {
            _logger.LogWarning(ex, "No GitLab integration for workspace {WorkspaceId}", workspaceId);
            return new ReviewToolResult { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ReviewMergeRequestAsync for workspace {WorkspaceId}", workspaceId);
            return new ReviewToolResult { Success = false, Error = ex.Message };
        }
    }
}
