namespace Orchestra.Application.CodeReview.Models;

/// <summary>
/// A single code-review finding produced by the review pipeline.
/// </summary>
public class ReviewFinding
{
    /// <summary>File path relative to the repository root.</summary>
    public string File { get; set; } = string.Empty;

    /// <summary>Affected line or range, e.g. "42" or "42-55".</summary>
    public string Lines { get; set; } = string.Empty;

    /// <summary>
    /// One of: contract-mismatch, logic-error, concurrency,
    /// resource-management, error-handling, security.
    /// </summary>
    public string BugCategory { get; set; } = string.Empty;

    /// <summary>Detailed explanation of the issue.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Optional unified-diff-style fix suggestion.
    /// Null when no suggestion was produced.
    /// </summary>
    public string? FixSuggestion { get; set; }
}

/// <summary>
/// Structured result returned to the parent agent LLM after a code review
/// tool call completes (success or failure).
/// </summary>
public class ReviewToolResult
{
    /// <summary>True when the review completed without an unhandled error.</summary>
    public bool Success { get; set; }

    /// <summary>
    /// Review verdict: APPROVED, REQUEST_CHANGES, or COMMENTED.
    /// Null when Success is false.
    /// </summary>
    public string? Verdict { get; set; }

    /// <summary>
    /// URL of the submitted review on the provider platform.
    /// Null when Success is false.
    /// </summary>
    public string? ReviewUrl { get; set; }

    /// <summary>
    /// Human-readable narrative of the overall review.
    /// Null when Success is false.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Per-file findings. Empty array when no issues were found
    /// (verdict will be APPROVED in that case).
    /// </summary>
    public ReviewFinding[] Findings { get; set; } = Array.Empty<ReviewFinding>();

    /// <summary>
    /// Human-readable error description.
    /// Populated only when Success is false; null otherwise.
    /// </summary>
    public string? Error { get; set; }
}
