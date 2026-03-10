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
}
