using Orchestra.Infrastructure.Tools;
using Orchestra.Infrastructure.Tools.Services;

namespace Orchestra.Infrastructure.Tests.Tools;

/// <summary>
/// Unit tests for <see cref="ReviewResultMarshaller.MarshalReviewResult"/>.
/// Covers all FR-05 BDD acceptance-criteria scenarios without requiring any mocked
/// dependencies — the method under test is a pure static function.
/// </summary>
public class ReviewResultMarshallerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds a minimal valid finding JSON object string.</summary>
    private static string FindingJson(
        string file = "src/Service.cs",
        string lines = "1",
        string bugCategory = "logic-error",
        string comment = "An issue.",
        string? fixSuggestion = null)
    {
        var fix = fixSuggestion == null ? "null" : $"\"{fixSuggestion}\"";
        return $$"""{"file":"{{file}}","lines":"{{lines}}","bugCategory":"{{bugCategory}}","comment":"{{comment}}","fixSuggestion":{{fix}}}""";
    }

    // ── Scenario 1: successful review with findings → REQUEST_CHANGES ─────────

    [Fact]
    public void MarshalReviewResult_WithFindingsJson_ReturnsSuccess_RequestChangesVerdict()
    {
        // Arrange
        var json = """
            {
              "success": true,
              "verdict": "REQUEST_CHANGES",
              "reviewUrl": "https://github.com/org/repo/pull/42#pullrequestreview-999",
              "summary": "Two issues were identified.",
              "findings": [
                {"file":"src/Auth.cs","lines":"42-55","bugCategory":"security","comment":"Injection risk.","fixSuggestion":null},
                {"file":"src/Service.cs","lines":"88","bugCategory":"error-handling","comment":"Missing null check.","fixSuggestion":null}
              ]
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("REQUEST_CHANGES", result.Verdict);
        Assert.Equal("https://github.com/org/repo/pull/42#pullrequestreview-999", result.ReviewUrl);
        Assert.NotNull(result.Summary);
        Assert.Equal(2, result.Findings.Length);
        Assert.Equal("src/Auth.cs", result.Findings[0].File);
        Assert.Equal("security", result.Findings[0].BugCategory);
        Assert.Null(result.Error);
    }

    // ── Scenario 2: successful review with no findings → APPROVED ────────────

    [Fact]
    public void MarshalReviewResult_WithEmptyFindingsJson_ReturnsSuccess_ApprovedVerdict()
    {
        // Arrange
        var json = """
            {
              "success": true,
              "verdict": "APPROVED",
              "reviewUrl": "https://github.com/org/repo/pull/10#pullrequestreview-1",
              "summary": "No issues found. The code meets the project standards.",
              "findings": []
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("APPROVED", result.Verdict);
        Assert.NotNull(result.ReviewUrl);
        Assert.Empty(result.Findings);
        Assert.Null(result.Error);
    }

    // ── Scenario 3: findings exceed 50 → truncated to 50 + notice in Summary ─

    [Fact]
    public void MarshalReviewResult_With73Findings_TruncatesTo50_AppendsNoticeToSummary()
    {
        // Arrange — build JSON with 73 distinct findings
        var findingsJson = string.Join(",\n",
            Enumerable.Range(1, 73).Select(i => FindingJson(
                file: $"src/File{i}.cs",
                lines: $"{i}",
                comment: $"Issue {i}.")));

        var json = $$"""
            {
              "success": true,
              "verdict": "REQUEST_CHANGES",
              "reviewUrl": "https://github.com/org/repo/pull/5#pullrequestreview-777",
              "summary": "Many issues found.",
              "findings": [{{findingsJson}}]
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.Equal(ReviewResultMarshaller.MaxFindings, result.Findings.Length);
        Assert.NotNull(result.Summary);
        Assert.Contains("23 additional findings", result.Summary);
        Assert.Contains("truncated", result.Summary);
    }

    // ── Scenario 4: PR/MR not found → error result, empty findings ───────────

    [Fact]
    public void MarshalReviewResult_WithNotFoundErrorJson_ReturnsFailure_ErrorPopulated_FindingsEmpty()
    {
        // Arrange
        var json = """
            {
              "success": false,
              "error": "Pull request / merge request not found.",
              "findings": []
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Pull request / merge request not found.", result.Error);
        Assert.Empty(result.Findings);
        Assert.Null(result.Verdict);
        Assert.Null(result.ReviewUrl);
        Assert.Null(result.Summary);
    }

    // ── Scenario 5: provider API unavailable → error result ──────────────────

    [Fact]
    public void MarshalReviewResult_WithApiUnavailableErrorJson_ReturnsFailure_ErrorPopulated()
    {
        // Arrange
        var json = """
            {
              "success": false,
              "error": "GitHub API unreachable. Please verify connectivity and retry.",
              "findings": []
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("unreachable", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Findings);
    }

    // ── Scenario 6: submission failure → Success=false, findings preserved ────

    [Fact]
    public void MarshalReviewResult_WithSubmissionFailureJson_ReturnsFailure_FindingsPreserved()
    {
        // Arrange
        var json = """
            {
              "success": false,
              "error": "Review submission failed: 403 Forbidden: Insufficient permissions to submit review",
              "findings": [
                {"file":"src/Auth.cs","lines":"88","bugCategory":"error-handling","comment":"Missing null check.","fixSuggestion":null}
              ]
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("Review submission failed", result.Error);
        Assert.Single(result.Findings);
        Assert.Equal("src/Auth.cs", result.Findings[0].File);
        Assert.Equal("error-handling", result.Findings[0].BugCategory);
        Assert.Null(result.Verdict);
        Assert.Null(result.ReviewUrl);
        Assert.Null(result.Summary);
    }

    // ── Empty response → error result ─────────────────────────────────────────

    [Fact]
    public void MarshalReviewResult_WithEmptyString_ReturnsFailure_EmptyResponseError()
    {
        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("empty response", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarshalReviewResult_WithWhitespaceOnly_ReturnsFailure_EmptyResponseError()
    {
        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult("   \n  ");

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("empty response", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Malformed JSON → fallback to text summary ─────────────────────────────

    [Fact]
    public void MarshalReviewResult_WithPureProseNoJson_FallsBackToTextSummary()
    {
        // Arrange
        var prose = "The review is complete. Everything looks good.";

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(prose);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(prose, result.Summary);
        Assert.Empty(result.Findings);
        Assert.Null(result.Error);
    }

    [Fact]
    public void MarshalReviewResult_WithMalformedJson_FallsBackToTextSummary()
    {
        // Arrange — a brace that starts JSON but is not a complete valid JSON object
        var malformed = "{ invalid json content %%";

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(malformed);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(malformed, result.Summary);
        Assert.Empty(result.Findings);
    }

    // ── FixSuggestion truncation at 2,000 characters ──────────────────────────

    [Fact]
    public void MarshalReviewResult_WithOversizedFixSuggestion_TruncatesToMaxLengthPlusEllipsis()
    {
        // Arrange — fixSuggestion is 2,500 characters (500 over the limit)
        var longFix = new string('x', 2_500);
        var findingJson = FindingJson(fixSuggestion: longFix);

        var json = $$"""
            {
              "success": true,
              "verdict": "REQUEST_CHANGES",
              "reviewUrl": null,
              "summary": "One issue found.",
              "findings": [{{findingJson}}]
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.Single(result.Findings);
        Assert.NotNull(result.Findings[0].FixSuggestion);
        // Expected length: 2000 chars + "..." (3 chars) = 2003
        Assert.Equal(ReviewResultMarshaller.MaxFixSuggestionLength + "...".Length,
            result.Findings[0].FixSuggestion!.Length);
        Assert.EndsWith("...", result.Findings[0].FixSuggestion);
    }

    [Fact]
    public void MarshalReviewResult_WithFixSuggestionAtExactLimit_DoesNotTruncate()
    {
        // Arrange — fixSuggestion is exactly 2,000 characters (at the limit, no truncation)
        var exactFix = new string('y', ReviewResultMarshaller.MaxFixSuggestionLength);
        var findingJson = FindingJson(fixSuggestion: exactFix);

        var json = $$"""
            {
              "success": true,
              "verdict": "REQUEST_CHANGES",
              "reviewUrl": null,
              "summary": "One issue found.",
              "findings": [{{findingJson}}]
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.Single(result.Findings);
        Assert.Equal(ReviewResultMarshaller.MaxFixSuggestionLength,
            result.Findings[0].FixSuggestion!.Length);
        Assert.False(result.Findings[0].FixSuggestion!.EndsWith("..."));
    }

    // ── Findings cap: truncation notice is suppressed when Summary is null ────

    [Fact]
    public void MarshalReviewResult_WithOver50FindingsAndNullSummary_TruncatesFindings_NoNoticeAdded()
    {
        // Arrange — submission failure path: success=false, summary=null, 51 findings
        var findingsJson = string.Join(",\n",
            Enumerable.Range(1, 51).Select(i => FindingJson(
                file: $"src/File{i}.cs",
                comment: $"Issue {i}.")));

        var json = $$"""
            {
              "success": false,
              "error": "Review submission failed: 403 Forbidden",
              "findings": [{{findingsJson}}]
            }
            """;

        // Act
        var result = ReviewResultMarshaller.MarshalReviewResult(json);

        // Assert
        Assert.Equal(ReviewResultMarshaller.MaxFindings, result.Findings.Length);
        Assert.Null(result.Summary);  // no summary to append notice to
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}
