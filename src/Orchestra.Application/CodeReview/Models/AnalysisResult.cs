namespace Orchestra.Application.CodeReview.Models;

/// <summary>
/// Structured result from the LLM code analysis pass.
/// </summary>
public record AnalysisResult
{
    public required string Summary { get; init; }
    public required List<ReviewFinding> Findings { get; init; }
}
