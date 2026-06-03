using Orchestra.Application.Common.Interfaces;
using System.Text.RegularExpressions;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Extracts image source references from markdown text using the standard
/// markdown image syntax: ![alt](source).
/// </summary>
public class TicketImageExtractor : ITicketImageExtractor
{
    // Matches: ![any alt text](source) where source is not empty, including paths with spaces
    private static readonly Regex ImagePattern =
        new(@"!\[[^\]]*\]\(([^)]+?)\s*\)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    public IReadOnlyList<string> Extract(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        foreach (Match match in ImagePattern.Matches(markdown))
        {
            var source = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(source) && seen.Add(source))
                results.Add(source);
        }

        return results;
    }
}
