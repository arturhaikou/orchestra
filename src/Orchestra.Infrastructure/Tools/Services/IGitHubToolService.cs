using System.ComponentModel;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tools.Services;

public class GitHubIssueResult
{
    public bool Success { get; set; }
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? State { get; set; }
    public string? Url { get; set; }
    public string[]? Assignees { get; set; }
    public string[]? Labels { get; set; }
    public string? Error { get; set; }
}

public class GitHubPullRequestResult
{
    public bool Success { get; set; }
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? State { get; set; }
    public bool Merged { get; set; }
    public string? Url { get; set; }
    public string? HeadBranch { get; set; }
    public string? BaseBranch { get; set; }
    public bool? Mergeable { get; set; }
    public string? Error { get; set; }
}

public class GitHubSearchIssuesResult
{
    public bool Success { get; set; }
    public int TotalCount { get; set; }
    public object[]? Items { get; set; }
    public string? Error { get; set; }
}

public class GitHubCreateIssueResult
{
    public bool Success { get; set; }
    public int Number { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Error { get; set; }
}

public class GitHubUpdateIssueResult
{
    public bool Success { get; set; }
    public int Number { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Error { get; set; }
}

public class GitHubCreatePullRequestResult
{
    public bool Success { get; set; }
    public int Number { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? HeadBranch { get; set; }
    public string? BaseBranch { get; set; }
    public bool Draft { get; set; }
    public string? Error { get; set; }
}

public class GitHubPushBranchResult
{
    public bool Success { get; set; }
    public string? Branch { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}

[ToolCategory("GitHub", ProviderType.GITHUB, "Interact with GitHub repositories")]
public interface IGitHubToolService
{
    [ToolAction("get_issue", "Get GitHub issue details", DangerLevel.Safe)]
    [Description("Retrieve detailed information about a GitHub issue by its number")]
    Task<GitHubIssueResult> GetIssueAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use. Required when the workspace has multiple GitHub integrations configured.")] string integrationId,
        [Description("The GitHub issue number")] string issueNumber);

    [ToolAction("get_pr", "Get pull request details", DangerLevel.Safe)]
    [Description("Retrieve detailed information about a GitHub pull request by its number")]
    Task<GitHubPullRequestResult> GetPullRequestAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use. Required when the workspace has multiple GitHub integrations configured.")] string integrationId,
        [Description("The GitHub pull request number")] string pullNumber);

    [ToolAction("search_issues", "Search GitHub issues", DangerLevel.Safe)]
    [Description("Search for issues in the workspace's connected GitHub repository by query text. Results are scoped to issues only.")]
    Task<GitHubSearchIssuesResult> SearchIssuesAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use. Required when the workspace has multiple GitHub integrations configured.")] string integrationId,
        [Description("The search query text")] string query,
        [Description("Maximum number of results to return (default 10, max 30)")] int? limit = 10);

    [ToolAction("create_issue", "Create a new GitHub issue", DangerLevel.Moderate)]
    [Description("Create a new GitHub issue in the workspace's connected repository")]
    Task<GitHubCreateIssueResult> CreateIssueAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use. Required when the workspace has multiple GitHub integrations configured.")] string integrationId,
        [Description("The issue title")] string title,
        [Description("The issue body/description")] string body);

    [ToolAction("update_issue", "Update an existing GitHub issue", DangerLevel.Moderate)]
    [Description("Update the title and/or body of an existing GitHub issue")]
    Task<GitHubUpdateIssueResult> UpdateIssueAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use. Required when the workspace has multiple GitHub integrations configured.")] string integrationId,
        [Description("The GitHub issue number")] string issueNumber,
        [Description("New title (optional; if null or empty, omit from patch)")] string? title,
        [Description("New body (optional; if null or empty, omit from patch)")] string? body);

    [ToolAction("push_branch", "Push Branch to Remote", DangerLevel.Moderate)]
    [Description("Push a local git branch to the GitHub remote using the integration credentials. Use this before creating a pull request when the branch has not yet been pushed.")]
    Task<GitHubPushBranchResult> PushBranchAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use.")] string integrationId,
        [Description("The ID of the CLI integration whose WorkingDirectory points to the local git repository. Choose from the [Available CLI Integrations] block in your context.")] string cliIntegrationId,
        [Description("The name of the branch to push")] string branchName);

    [ToolAction("create_pr", "Create Pull Request", DangerLevel.Moderate)]
    [Description("Create a new pull request in the workspace's connected GitHub repository")]
    Task<GitHubCreatePullRequestResult> CreatePullRequestAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use. Required when the workspace has multiple GitHub integrations configured.")] string integrationId,
        [Description("The pull request title")] string title,
        [Description("The pull request body/description")] string body,
        [Description("The name of the branch where changes are implemented (head branch)")] string headBranch,
        [Description("The name of the branch you want the changes pulled into (base branch, e.g. 'main')")] string baseBranch,
        [Description("Whether to create the pull request as a draft (default: false)")] bool draft = false);

    [ToolAction("review_pull_request", "Review Pull Request", DangerLevel.Moderate)]
    [Description("Performs an automated code review of a GitHub pull request, analysing the diff and submitting structured findings.")]
    Task<ReviewToolResult> ReviewPullRequestAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitHub integration instance to use. Required when the workspace has multiple GitHub integrations configured.")] string integrationId,
        [Description("The GitHub pull request number")] string pullNumber,
        string? modelIdentifier = null,
        string? projectPrinciples = null);
}