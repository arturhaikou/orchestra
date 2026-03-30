using Orchestra.Application.CodeReview;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitHub.Models;

namespace Orchestra.Infrastructure.CodeReview.Providers;

/// <summary>
/// GitHub implementation of <see cref="ICodeReviewProvider"/>.
/// Maps GitHub API models to normalized models and submits reviews
/// via the single-POST reviews API.
/// </summary>
public class GitHubReviewProvider : ICodeReviewProvider
{
    private readonly IGitHubApiClient _apiClient;

    public GitHubReviewProvider(IGitHubApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ProviderType ProviderType => ProviderType.GITHUB;

    public async Task<List<NormalizedFileDiff>> FetchChangedFilesAsync(
        string prOrMrNumber, CancellationToken cancellationToken)
    {
        var prNumber = int.Parse(prOrMrNumber);
        var files = await _apiClient.GetPullRequestFilesAsync(prNumber, cancellationToken);

        return files.Select(f => new NormalizedFileDiff
        {
            Path = f.Filename,
            Status = MapStatus(f.Status),
            Additions = f.Additions,
            Deletions = f.Deletions,
            Patch = f.Patch ?? string.Empty,
        }).ToList();
    }

    public async Task<string?> FetchFileContentAsync(
        string path, string? gitRef, CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient.GetFileContentAsync(path, gitRef, cancellationToken);
        }
        catch
        {
            // File may not exist at the given ref (deleted files, etc.)
            return null;
        }
    }

    public async Task<SubmissionResult> SubmitReviewAsync(
        ReviewSubmission submission, CancellationToken cancellationToken)
    {
        var prNumber = int.Parse(submission.PrOrMrNumber);

        var reviewEvent = submission.Verdict switch
        {
            "APPROVED" => "APPROVE",
            "REQUEST_CHANGES" => "REQUEST_CHANGES",
            _ => "COMMENT"
        };

        var comments = submission.Findings
            .Where(f => !string.IsNullOrEmpty(f.Lines))
            .Select(f => new GitHubInlineReviewComment
            {
                Path = f.File,
                Line = ParseFirstLine(f.Lines),
                Side = "RIGHT",
                Body = FormatFindingBody(f),
            })
            .ToList();

        try
        {
            var result = await _apiClient.SubmitPullRequestReviewAsync(
                prNumber,
                reviewEvent,
                submission.Summary,
                comments.Count > 0 ? comments : null,
                cancellationToken);

            return new SubmissionResult
            {
                Success = true,
                Url = result.HtmlUrl,
            };
        }
        catch (Exception ex)
        {
            return new SubmissionResult
            {
                Success = false,
                Error = $"Review submission failed: {ex.Message}",
            };
        }
    }

    private static string MapStatus(string githubStatus) => githubStatus.ToLowerInvariant() switch
    {
        "added" => "added",
        "removed" => "deleted",
        "modified" or "changed" => "modified",
        "renamed" => "renamed",
        _ => "modified",
    };

    private static int ParseFirstLine(string lines)
    {
        var dashIndex = lines.IndexOf('-');
        var linePart = dashIndex >= 0 ? lines[..dashIndex] : lines;
        return int.TryParse(linePart, out var line) ? line : 1;
    }

    private static string FormatFindingBody(ReviewFinding finding)
    {
        var body = $"**[{finding.BugCategory}]** {finding.Comment}";
        if (!string.IsNullOrEmpty(finding.FixSuggestion))
        {
            body += $"\n\n```suggestion\n{finding.FixSuggestion}\n```";
        }
        return body;
    }
}
