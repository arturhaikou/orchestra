using Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab;

public interface IGitLabApiClient
{
    Task<(List<GitLabIssue> Issues, bool HasNextPage)> GetProjectIssuesAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default);
    Task<GitLabIssue?> GetIssueAsync(int iid, CancellationToken cancellationToken = default);
    Task<List<GitLabNote>> GetIssueNotesAsync(int iid, CancellationToken cancellationToken = default);
    Task<GitLabNote> AddNoteAsync(int iid, string body, CancellationToken cancellationToken = default);
    Task<GitLabIssue> CreateIssueAsync(string title, string description, List<string>? labels = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves detailed information about a specific GitLab merge request by its IID (project-scoped ID).
    /// </summary>
    /// <param name="mrIid">The merge request IID (internal ID, scoped to the project)</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>A GitLabMergeRequest object with merge request details, or null if not found</returns>
    Task<GitLabMergeRequest?> GetMergeRequestAsync(int mrIid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for GitLab issues matching the provided query text within the project.
    /// </summary>
    /// <param name="query">The search query text (e.g., "login bug", "feature request")</param>
    /// <param name="limit">Maximum number of results to return; defaults to 10, capped at 30 per GitLab API</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>A list of GitLabIssue objects matching the search query</returns>
    Task<List<GitLabIssue>> SearchIssuesAsync(string query, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing GitLab issue's title and/or description.
    /// </summary>
    /// <param name="iid">The issue IID (project-scoped ID)</param>
    /// <param name="title">The new title for the issue; if null or whitespace, the title is not updated</param>
    /// <param name="description">The new description for the issue; if null, the description is not updated</param>
    /// <param name="cancellationToken">Cancellation token for the async operation</param>
    /// <returns>The updated GitLabIssue object</returns>
    Task<GitLabIssue> UpdateIssueAsync(int iid, string? title, string? description, CancellationToken cancellationToken = default);

    // ── Review sub-agent methods ──────────────────────────────────────────────
    // These methods carry no [ToolAction] attribute. They are never discovered
    // by ToolScanningService and are therefore structurally absent from the
    // tool catalogue. They exist solely for use by the Code Review Orchestration
    // Service when constructing sub-agent AIFunction instances at runtime.

    Task<string> GetMergeRequestDiffAsync(int mrIid, CancellationToken cancellationToken = default);

    Task<GitLabMrChangesResult> GetMergeRequestChangesAsync(int mrIid, CancellationToken cancellationToken = default);

    Task<GitLabNote> SubmitMergeRequestNoteAsync(int mrIid, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an inline discussion thread on the specified merge request at the given
    /// diff position. Returns a result object instead of throwing so that a
    /// position-invalid call for one finding does not abort the review submission loop.
    /// </summary>
    /// <param name="mrIid">The merge request IID (project-scoped).</param>
    /// <param name="body">
    /// The discussion body text. When a fix suggestion is present, it must be appended
    /// using a GitLab suggestion fenced block: ```suggestion:-0+0⏎{suggestion}⏎```.
    /// </param>
    /// <param name="baseSha">
    /// base_commit_sha from GetMergeRequestChangesAsync. Must not be empty.
    /// </param>
    /// <param name="startSha">
    /// start_commit_sha from GetMergeRequestChangesAsync. Must not be empty.
    /// </param>
    /// <param name="headSha">
    /// head_commit_sha from GetMergeRequestChangesAsync. Must not be empty.
    /// </param>
    /// <param name="oldPath">File path before the change (same as newPath for non-renames).</param>
    /// <param name="newPath">File path after the change.</param>
    /// <param name="oldLine">
    /// Line number in the old file version. Null when commenting on a purely added line.
    /// </param>
    /// <param name="newLine">
    /// Line number in the new file version. Null when commenting on a purely removed line.
    /// </param>
    Task<GitLabDiscussionResult> CreateMergeRequestDiscussionAsync(
        int mrIid,
        string body,
        string baseSha,
        string startSha,
        string headSha,
        string oldPath,
        string newPath,
        int? oldLine,
        int? newLine,
        CancellationToken cancellationToken = default);

    Task<GitLabApproval> ApproveMergeRequestAsync(int mrIid, CancellationToken cancellationToken = default);
}
