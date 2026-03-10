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
}
