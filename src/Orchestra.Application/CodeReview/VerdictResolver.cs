using Orchestra.Application.CodeReview.Models;

namespace Orchestra.Application.CodeReview;

/// <summary>
/// Deterministic verdict resolution. Findings present = REQUEST_CHANGES;
/// no findings = APPROVED. The LLM no longer decides the verdict.
/// </summary>
public static class VerdictResolver
{
    public static string Resolve(List<ReviewFinding> findings)
    {
        if (findings.Count == 0) return "APPROVED";
        return "REQUEST_CHANGES";
    }
}
