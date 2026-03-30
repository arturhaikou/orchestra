namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

/// <summary>
/// Result returned by <c>GetMergeRequestChangesAsync</c>. Combines the list of
/// per-file diff changes with the MR version SHA metadata needed to construct
/// inline discussion position objects.
/// </summary>
public class GitLabMrChangesResult
{
    /// <summary>
    /// The base_commit_sha from the latest MR version; identifies the common
    /// ancestor commit shared by source and target branch.
    /// </summary>
    public string BaseSha { get; set; } = string.Empty;

    /// <summary>
    /// The start_commit_sha from the latest MR version; identifies the target
    /// branch HEAD at the time the MR was created.
    /// </summary>
    public string StartSha { get; set; } = string.Empty;

    /// <summary>
    /// The head_commit_sha from the latest MR version; identifies the latest
    /// HEAD of the source branch.
    /// </summary>
    public string HeadSha { get; set; } = string.Empty;

    /// <summary>Per-file changes with unified diff patches.</summary>
    public List<GitLabMergeRequestChange> Changes { get; set; } = new();
}
