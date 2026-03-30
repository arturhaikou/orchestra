using System.Text.RegularExpressions;
using Orchestra.Application.CodeReview;
using Orchestra.Application.CodeReview.Models;

namespace Orchestra.Infrastructure.CodeReview;

/// <summary>
/// Regex-based heuristic that detects method/function signature changes
/// in unified diff patches. Works across languages by matching common
/// function definition patterns on modified diff lines.
/// </summary>
public class RegexSignatureChangeDetector : ISignatureChangeDetector
{
    // Common method/function definition patterns across popular languages.
    // Matches lines that begin with +/- (diff markers) followed by a definition.
    private static readonly Regex[] SignaturePatterns =
    [
        // C#, Java, TypeScript: public/private/protected/internal + return_type + name(
        new Regex(@"^[+-]\s*(?:public|private|protected|internal|static|async|override|virtual|abstract)\s+\S+\s+(\w+)\s*\(", RegexOptions.Compiled),
        // Python: def name(
        new Regex(@"^[+-]\s*(?:async\s+)?def\s+(\w+)\s*\(", RegexOptions.Compiled),
        // JavaScript/TypeScript: function name( or name(
        new Regex(@"^[+-]\s*(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\(", RegexOptions.Compiled),
        // Go: func name( or func (receiver) name(
        new Regex(@"^[+-]\s*func\s+(?:\([^)]*\)\s+)?(\w+)\s*\(", RegexOptions.Compiled),
        // Rust: fn name(
        new Regex(@"^[+-]\s*(?:pub\s+)?(?:async\s+)?fn\s+(\w+)\s*\(", RegexOptions.Compiled),
    ];

    public List<SignatureChange> Detect(List<NormalizedFileDiff> diffs)
    {
        var changes = new List<SignatureChange>();

        foreach (var diff in diffs)
        {
            if (string.IsNullOrEmpty(diff.Patch))
                continue;

            var lines = diff.Patch.Split('\n');
            var removedMethods = new HashSet<string>();
            var addedMethods = new HashSet<string>();

            foreach (var line in lines)
            {
                if (line.Length == 0) continue;

                foreach (var pattern in SignaturePatterns)
                {
                    var match = pattern.Match(line);
                    if (!match.Success) continue;

                    var methodName = match.Groups[1].Value;

                    if (line[0] == '-')
                        removedMethods.Add(methodName);
                    else if (line[0] == '+')
                        addedMethods.Add(methodName);
                }
            }

            // A method that appears in both removed and added lines has had its
            // signature modified (parameters, return type, or access modifier changed).
            foreach (var method in removedMethods.Intersect(addedMethods))
            {
                changes.Add(new SignatureChange
                {
                    FilePath = diff.Path,
                    MethodName = method,
                    ChangeDescription = "Method signature was modified in the diff",
                });
            }
        }

        return changes;
    }
}
