namespace Orchestra.Application.CodeReview.Models;

/// <summary>
/// Provider-agnostic review submission payload.
/// </summary>
public record ReviewSubmission
{
    public required string PrOrMrNumber { get; init; }
    public required string Verdict { get; init; }
    public required string Summary { get; init; }
    public required ReviewFinding[] Findings { get; init; }
}
