namespace Orchestra.Application.CodeReview.Models;

/// <summary>
/// Result of submitting a review to a code hosting provider.
/// </summary>
public record SubmissionResult
{
    public required bool Success { get; init; }
    public string? Url { get; init; }
    public string? Error { get; init; }
}
