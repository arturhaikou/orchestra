using Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub;

public interface IGitHubApiClient
{
    Task<(List<GitHubIssue> Issues, bool HasNextPage)> GetRepositoryIssuesAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default);

    /// <summary>Throws InvalidOperationException for 401/403/404/429. Throws HttpRequestException(SocketException) for network failures.</summary>
    Task<GitHubIssue> GetIssueAsync(int issueNumber, CancellationToken cancellationToken = default);

    Task<List<GitHubComment>> GetIssueCommentsAsync(int issueNumber, CancellationToken cancellationToken = default);
    Task<GitHubComment> AddCommentAsync(int issueNumber, string commentBody, CancellationToken cancellationToken = default);
    Task<GitHubIssue> CreateIssueAsync(string title, string body, List<string>? labels = null, CancellationToken cancellationToken = default);
    Task<GitHubIssue> UpdateIssueAsync(int issueNumber, string? title, string? body, CancellationToken cancellationToken = default);

    /// <summary>Throws InvalidOperationException for 401/403/404/429. Throws HttpRequestException(SocketException) for network failures.</summary>
    Task<GitHubPullRequest> GetPullRequestAsync(int pullNumber, CancellationToken cancellationToken = default);

    /// <summary>Throws InvalidOperationException for 401/403/429. Throws HttpRequestException(SocketException) for network failures.</summary>
    Task<GitHubSearchResult> SearchIssuesAsync(string query, int limit, CancellationToken cancellationToken = default);

    // ── Review sub-agent methods ──────────────────────────────────────────────
    // These methods carry no [ToolAction] attribute. They are never discovered
    // by ToolScanningService and are therefore structurally absent from the
    // tool catalogue. They exist solely for use by the Code Review Orchestration
    // Service when constructing sub-agent AIFunction instances at runtime.

    Task<string> GetPullRequestDiffAsync(int prNumber, CancellationToken cancellationToken = default);

    Task<List<GitHubPullRequestFile>> GetPullRequestFilesAsync(int prNumber, CancellationToken cancellationToken = default);

    Task<List<GitHubReviewComment>> GetPullRequestReviewCommentsAsync(int prNumber, CancellationToken cancellationToken = default);

    Task<GitHubReviewSubmissionResult> SubmitPullRequestReviewAsync(int prNumber, string reviewEvent, string body, IReadOnlyList<GitHubInlineReviewComment>? comments = null, CancellationToken cancellationToken = default);

    Task<string> GetFileContentAsync(string path, string? gitRef, CancellationToken cancellationToken = default);

    Task<GitHubCreatedPullRequest> CreatePullRequestAsync(string title, string body, string headBranch, string baseBranch, bool draft = false, CancellationToken cancellationToken = default);
}
