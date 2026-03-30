using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

/// <summary>
/// Represents a single entry from the GitLab MR versions endpoint:
/// GET /api/v4/projects/:id/merge_requests/:iid/versions
/// The first element in the returned array is the latest version.
/// </summary>
public class GitLabMrVersion
{
    [JsonPropertyName("head_commit_sha")]
    public string HeadCommitSha { get; set; } = string.Empty;

    [JsonPropertyName("base_commit_sha")]
    public string BaseCommitSha { get; set; } = string.Empty;

    [JsonPropertyName("start_commit_sha")]
    public string StartCommitSha { get; set; } = string.Empty;
}
