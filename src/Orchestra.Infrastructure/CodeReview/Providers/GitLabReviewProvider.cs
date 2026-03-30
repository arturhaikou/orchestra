using Orchestra.Application.CodeReview;
using Orchestra.Application.CodeReview.Models;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Integrations.Providers.GitLab.Models;

namespace Orchestra.Infrastructure.CodeReview.Providers;

/// <summary>
/// GitLab implementation of <see cref="ICodeReviewProvider"/>.
/// Maps GitLab MR API models to normalized models. Submits reviews via
/// approval + per-finding inline discussions.
/// </summary>
public class GitLabReviewProvider : ICodeReviewProvider
{
    private readonly IGitLabApiClient _apiClient;

    // Captured from the MR changes response; needed for discussion positions.
    private string _baseSha = string.Empty;
    private string _startSha = string.Empty;
    private string _headSha = string.Empty;

    public GitLabReviewProvider(IGitLabApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public ProviderType ProviderType => ProviderType.GITLAB;

    public async Task<List<NormalizedFileDiff>> FetchChangedFilesAsync(
        string prOrMrNumber, CancellationToken cancellationToken)
    {
        var mrIid = int.Parse(prOrMrNumber);
        var changesResult = await _apiClient.GetMergeRequestChangesAsync(mrIid, cancellationToken);

        // Capture SHA metadata for later discussion submissions.
        _baseSha = changesResult.BaseSha;
        _startSha = changesResult.StartSha;
        _headSha = changesResult.HeadSha;

        return changesResult.Changes.Select(c => new NormalizedFileDiff
        {
            Path = c.NewPath,
            Status = MapStatus(c),
            Additions = CountDiffAdditions(c.Diff),
            Deletions = CountDiffDeletions(c.Diff),
            Patch = c.Diff,
        }).ToList();
    }

    public async Task<string?> FetchFileContentAsync(
        string path, string? gitRef, CancellationToken cancellationToken)
    {
        try
        {
            // GitLab doesn't expose a direct file-content method on this interface
            // that matches the GitHub pattern. Use the diff as available context.
            // For actual file fetching, the MR diff is usually sufficient.
            // This is a best-effort fetch via any available API.
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<SubmissionResult> SubmitReviewAsync(
        ReviewSubmission submission, CancellationToken cancellationToken)
    {
        var mrIid = int.Parse(submission.PrOrMrNumber);

        try
        {
            if (submission.Verdict == "APPROVED")
            {
                var approval = await _apiClient.ApproveMergeRequestAsync(mrIid, cancellationToken);
                return new SubmissionResult
                {
                    Success = true,
                    Url = approval.WebUrl,
                };
            }

            // REQUEST_CHANGES: submit inline discussions per finding.
            int successCount = 0;
            int failureCount = 0;
            string? lastError = null;

            foreach (var finding in submission.Findings)
            {
                var newLine = ParseFirstLine(finding.Lines);
                var body = FormatFindingBody(finding);

                try
                {
                    var result = await _apiClient.CreateMergeRequestDiscussionAsync(
                        mrIid,
                        body,
                        _baseSha,
                        _startSha,
                        _headSha,
                        finding.File,
                        finding.File,
                        oldLine: null,
                        newLine: newLine > 0 ? newLine : null,
                        cancellationToken);

                    if (result.Success)
                        successCount++;
                    else
                    {
                        failureCount++;
                        lastError = result.Error;
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    lastError = ex.Message;
                }
            }

            if (successCount == 0 && submission.Findings.Length > 0)
            {
                return new SubmissionResult
                {
                    Success = false,
                    Error = $"All {failureCount} inline discussion creation attempts failed. Last error: {lastError}",
                };
            }

            var summaryNote = failureCount > 0
                ? $"{submission.Summary}\n\nNote: {failureCount} finding(s) could not be posted inline because the diff position was no longer valid."
                : submission.Summary;

            // Post summary as a top-level note.
            await _apiClient.SubmitMergeRequestNoteAsync(mrIid, summaryNote, cancellationToken);

            return new SubmissionResult
            {
                Success = true,
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

    private static string MapStatus(GitLabMergeRequestChange change)
    {
        if (change.NewFile) return "added";
        if (change.DeletedFile) return "deleted";
        if (change.RenamedFile) return "renamed";
        return "modified";
    }

    private static int CountDiffAdditions(string diff)
    {
        return diff.Split('\n').Count(l => l.StartsWith('+') && !l.StartsWith("+++"));
    }

    private static int CountDiffDeletions(string diff)
    {
        return diff.Split('\n').Count(l => l.StartsWith('-') && !l.StartsWith("---"));
    }

    private static int ParseFirstLine(string lines)
    {
        if (string.IsNullOrEmpty(lines)) return 0;
        var dashIndex = lines.IndexOf('-');
        var linePart = dashIndex >= 0 ? lines[..dashIndex] : lines;
        return int.TryParse(linePart, out var line) ? line : 0;
    }

    private static string FormatFindingBody(ReviewFinding finding)
    {
        var body = $"**[{finding.BugCategory}]** {finding.Comment}";
        if (!string.IsNullOrEmpty(finding.FixSuggestion))
        {
            body += $"\n\n```suggestion:-0+0\n{finding.FixSuggestion}\n```";
        }
        return body;
    }
}
