using Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.GitHub;

public interface IGitHubApiClient
{
    Task<(List<GitHubIssue> Issues, bool HasNextPage)> GetRepositoryIssuesAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default);
    Task<GitHubIssue?> GetIssueAsync(int issueNumber, CancellationToken cancellationToken = default);
    Task<List<GitHubComment>> GetIssueCommentsAsync(int issueNumber, CancellationToken cancellationToken = default);
    Task<GitHubComment> AddCommentAsync(int issueNumber, string commentBody, CancellationToken cancellationToken = default);
    Task<GitHubIssue> CreateIssueAsync(string title, string body, List<string>? labels = null, CancellationToken cancellationToken = default);
}
