namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Extracts image source references (URLs and file paths) from markdown text
/// using standard markdown image syntax: ![alt](source).
/// </summary>
public interface ITicketImageExtractor
{
    /// <summary>
    /// Parses all image sources from the given markdown string.
    /// </summary>
    /// <param name="markdown">Markdown text that may contain image syntax.</param>
    /// <returns>
    /// Distinct list of raw source strings — absolute file paths (e.g. "C:\uploads\img.png")
    /// or URLs (e.g. "https://jira/rest/api/3/attachment/content/123").
    /// Returns an empty list when no images are found.
    /// </returns>
    IReadOnlyList<string> Extract(string markdown);
}
