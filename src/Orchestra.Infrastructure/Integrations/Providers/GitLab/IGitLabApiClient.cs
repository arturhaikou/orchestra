using Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab;

public interface IGitLabApiClient
{
    Task<List<GitLabIssue>> GetProjectIssuesAsync(int page = 1, int perPage = 30, CancellationToken cancellationToken = default);
    Task<GitLabIssue?> GetIssueAsync(int iid, CancellationToken cancellationToken = default);
    Task<List<GitLabNote>> GetIssueNotesAsync(int iid, CancellationToken cancellationToken = default);
    Task<GitLabNote> AddNoteAsync(int iid, string body, CancellationToken cancellationToken = default);
    Task<GitLabIssue> CreateIssueAsync(string title, string description, List<string>? labels = null, CancellationToken cancellationToken = default);
}
