using Orchestra.Application.CodeReview.Models;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.CodeReview;

/// <summary>
/// Abstracts provider-specific (GitHub/GitLab) code review API operations
/// behind a common interface. All return models are provider-agnostic.
/// </summary>
public interface ICodeReviewProvider
{
    ProviderType ProviderType { get; }

    /// <summary>
    /// Fetches changed files with their diffs, normalized to a common model.
    /// </summary>
    Task<List<NormalizedFileDiff>> FetchChangedFilesAsync(
        string prOrMrNumber, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches full file content at the head ref (for caller/callee context).
    /// Returns null if the file cannot be retrieved.
    /// </summary>
    Task<string?> FetchFileContentAsync(
        string path, string? gitRef, CancellationToken cancellationToken);

    /// <summary>
    /// Submits the review to the provider using its native API.
    /// </summary>
    Task<SubmissionResult> SubmitReviewAsync(
        ReviewSubmission submission, CancellationToken cancellationToken);
}
