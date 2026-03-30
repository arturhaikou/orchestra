namespace Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

/// <summary>
/// Structured result returned to the sub-agent LLM by
/// <c>CreateMergeRequestDiscussionAsync</c>. A result object is used instead
/// of throwing so that a single position-invalid call does not abort the
/// entire per-finding submission loop.
/// </summary>
public class GitLabDiscussionResult
{
    /// <summary>True when the discussion thread was successfully created.</summary>
    public bool Success { get; set; }

    /// <summary>
    /// The SHA-based string ID of the created discussion.
    /// Populated only when <see cref="Success"/> is true.
    /// </summary>
    public string? DiscussionId { get; set; }

    /// <summary>
    /// The integer ID of the root note inside the discussion.
    /// Populated only when <see cref="Success"/> is true.
    /// </summary>
    public int? NoteId { get; set; }

    /// <summary>
    /// Human-readable error description.
    /// Populated only when <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; set; }
}
