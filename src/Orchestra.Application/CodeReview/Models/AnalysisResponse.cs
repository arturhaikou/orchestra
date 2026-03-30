namespace Orchestra.Application.CodeReview.Models;

/// <summary>
/// Schema for the structured LLM response from <c>GetResponseAsync&lt;T&gt;</c>.
/// The LLM populates either findings or a context request, never both.
/// </summary>
public class AnalysisResponse
{
    public string Summary { get; set; } = string.Empty;
    public List<ReviewFinding> Findings { get; set; } = [];
    public bool NeedsAdditionalContext { get; set; }
    public List<FileContextRequest>? RequestedFiles { get; set; }
}
