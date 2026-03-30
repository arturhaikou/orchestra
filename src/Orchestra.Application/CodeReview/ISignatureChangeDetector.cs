using Orchestra.Application.CodeReview.Models;

namespace Orchestra.Application.CodeReview;

/// <summary>
/// Regex-based heuristic that detects method/function signature changes
/// in unified diff patches. Not an AST parser — works across languages.
/// </summary>
public interface ISignatureChangeDetector
{
    List<SignatureChange> Detect(List<NormalizedFileDiff> diffs);
}
