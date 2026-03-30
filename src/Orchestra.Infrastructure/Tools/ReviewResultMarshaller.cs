using System.Text.Json;
using Orchestra.Application.CodeReview.Models;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// Marshals the raw text output produced by the code-review sub-agent into a
/// <see cref="ReviewToolResult"/>.
/// </summary>
/// <remarks>
/// Applies two server-side guards after deserialisation:
/// <list type="bullet">
///   <item>Findings array is capped at <see cref="MaxFindings"/> items; a truncation notice
///   is appended to <see cref="ReviewToolResult.Summary"/> when it is non-null.</item>
///   <item>Each <see cref="ReviewFinding.FixSuggestion"/> is truncated at
///   <see cref="MaxFixSuggestionLength"/> characters.</item>
/// </list>
/// This class is <c>internal</c> and visible to <c>Orchestra.Infrastructure.Tests</c>
/// via the <c>InternalsVisibleTo</c> attribute in the project file.
/// </remarks>
internal static class ReviewResultMarshaller
{
    internal const int MaxFindings = 50;
    internal const int MaxFixSuggestionLength = 2_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses <paramref name="responseText"/> (the sub-agent's final output) into a
    /// <see cref="ReviewToolResult"/>. Applies the 50-finding cap and
    /// <see cref="ReviewFinding.FixSuggestion"/> truncation as server-side guards.
    /// Never throws.
    /// </summary>
    /// <param name="responseText">
    /// The raw text output emitted by the code-review sub-agent. Expected to contain
    /// a JSON object matching the <see cref="ReviewToolResult"/> schema; may include
    /// surrounding prose.
    /// </param>
    /// <returns>A fully populated <see cref="ReviewToolResult"/>.</returns>
    internal static ReviewToolResult MarshalReviewResult(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return new ReviewToolResult
            {
                Success = false,
                Error = "Sub-agent returned an empty response.",
            };
        }

        ReviewToolResult? result = null;

        try
        {
            // Extract the outermost JSON object from the response; the sub-agent may
            // include preamble or trailing prose before/after the JSON block.
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd   = responseText.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonFragment = responseText[jsonStart..(jsonEnd + 1)];
                result = JsonSerializer.Deserialize<ReviewToolResult>(jsonFragment, JsonOptions);
            }
        }
        catch
        {
            // JSON extraction failed — fall through to the text-based fallback.
        }

        if (result == null)
        {
            // Fallback: treat the entire response as the review summary.
            return new ReviewToolResult
            {
                Success = true,
                Summary = responseText,
                Findings = Array.Empty<ReviewFinding>(),
            };
        }

        // Apply server-side guards after successful deserialisation.
        ApplyFindingsCap(result);
        TruncateFixSuggestions(result);

        return result;
    }

    /// <summary>
    /// Enforces the <see cref="MaxFindings"/> cap. When the result contains more than
    /// <see cref="MaxFindings"/> items, the array is truncated to the first
    /// <see cref="MaxFindings"/> entries and a notice is appended to
    /// <see cref="ReviewToolResult.Summary"/> when it is non-null.
    /// </summary>
    private static void ApplyFindingsCap(ReviewToolResult result)
    {
        if (result.Findings.Length <= MaxFindings)
            return;

        var overflow = result.Findings.Length - MaxFindings;
        result.Findings = result.Findings[..MaxFindings];

        if (result.Summary != null)
        {
            result.Summary +=
                $" Note: {overflow} additional findings were identified but have been truncated due to result size limits.";
        }
    }

    /// <summary>
    /// Truncates each <see cref="ReviewFinding.FixSuggestion"/> that exceeds
    /// <see cref="MaxFixSuggestionLength"/> characters. Appends "..." to indicate
    /// the content was cut.
    /// </summary>
    private static void TruncateFixSuggestions(ReviewToolResult result)
    {
        foreach (var finding in result.Findings)
        {
            if (finding.FixSuggestion?.Length > MaxFixSuggestionLength)
                finding.FixSuggestion = finding.FixSuggestion[..MaxFixSuggestionLength] + "...";
        }
    }
}
