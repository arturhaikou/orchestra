using System.ComponentModel;
using System.Threading.Tasks;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tools.Services;

public class GitLabIssueResult
{
    public bool Success { get; set; }
    public int Iid { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? State { get; set; }
    public string? Url { get; set; }
    public string? Error { get; set; }
}

public class GitLabMergeRequestResult
{
    public bool Success { get; set; }
    public int Iid { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? State { get; set; }
    public bool Merged { get; set; }
    public string? Url { get; set; }
    public string? SourceBranch { get; set; }
    public string? TargetBranch { get; set; }
    public string? Error { get; set; }
}

public class GitLabSearchIssuesResult
{
    public bool Success { get; set; }
    public int TotalCount { get; set; }
    public object[]? Items { get; set; }
    public string? Error { get; set; }
}

public class GitLabCreateIssueResult
{
    public bool Success { get; set; }
    public int Iid { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Error { get; set; }
}

public class GitLabUpdateIssueResult
{
    public bool Success { get; set; }
    public int Iid { get; set; }
    public string? Url { get; set; }
    public string? Title { get; set; }
    public string? Error { get; set; }
}

[ToolCategory("GitLab", ProviderType.GITLAB, "Interact with GitLab repositories and issues")]
public interface IGitLabToolService
{
    [ToolAction("get_issue", "Get Issue", DangerLevel.Safe)]
    [Description("Retrieve detailed information about a GitLab issue by its IID")]
    Task<GitLabIssueResult> GetIssueAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitLab integration instance to use. Required when the workspace has multiple GitLab integrations configured.")] string integrationId,
        [Description("The GitLab issue IID (project-scoped ID)")] string issueIid);

    [ToolAction("get_mr", "Get Merge Request", DangerLevel.Safe)]
    [Description("Retrieve detailed information about a GitLab merge request by its IID")]
    Task<GitLabMergeRequestResult> GetMergeRequestAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitLab integration instance to use. Required when the workspace has multiple GitLab integrations configured.")] string integrationId,
        [Description("The GitLab merge request IID (project-scoped ID)")] string mrIid);

    [ToolAction("search_issues", "Search Issues", DangerLevel.Safe)]
    [Description("Search for issues in the workspace's connected GitLab repository by query text")]
    Task<GitLabSearchIssuesResult> SearchIssuesAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitLab integration instance to use. Required when the workspace has multiple GitLab integrations configured.")] string integrationId,
        [Description("The search query text")] string query,
        [Description("Maximum number of results to return (default 10, max 30)")] int? limit = 10);

    [ToolAction("create_issue", "Create Issue", DangerLevel.Moderate)]
    [Description("Create a new issue in the workspace's connected GitLab repository")]
    Task<GitLabCreateIssueResult> CreateIssueAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitLab integration instance to use. Required when the workspace has multiple GitLab integrations configured.")] string integrationId,
        [Description("The issue title")] string title,
        [Description("The issue description")] string description);

    [ToolAction("update_issue", "Update Issue", DangerLevel.Moderate)]
    [Description("Update the title and/or description of an existing GitLab issue")]
    Task<GitLabUpdateIssueResult> UpdateIssueAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitLab integration instance to use. Required when the workspace has multiple GitLab integrations configured.")] string integrationId,
        [Description("The GitLab issue IID (project-scoped ID)")] string issueIid,
        [Description("New title (optional; if null or empty, omit from update)")] string? title,
        [Description("New description (optional; if null or empty, omit from update)")] string? description);

    [ToolAction("review_merge_request", "Review Merge Request", DangerLevel.Moderate)]
    [Description("Performs an automated code review of a GitLab merge request, analysing the diff and submitting structured findings.")]
    Task<ReviewToolResult> ReviewMergeRequestAsync(
        [Description("The workspace ID (GUID)")] string workspaceId,
        [Description("The ID of the specific GitLab integration instance to use. Required when the workspace has multiple GitLab integrations configured.")] string integrationId,
        [Description("The GitLab merge request IID (project-scoped ID)")] string mrIid,
        string? modelIdentifier = null,
        string? projectPrinciples = null);
}
