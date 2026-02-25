using System.Text.RegularExpressions;

namespace Orchestra.Domain.Validators;

public static class FilterQueryValidator
{
    /// <summary>
    /// Validates that a Jira filter query (JQL) contains the "project" keyword exactly once
    /// and does not use "in" or "not in" operators (which would allow multiple projects).
    /// </summary>
    /// <param name="filterQuery">The JQL filter query to validate.</param>
    /// <exception cref="ArgumentException">
    /// Throws when filterQuery is null, empty, does not contain "project" exactly once,
    /// or uses "in" or "not in" operators.
    /// </exception>
    public static void ValidateJiraFilterQuery(string? filterQuery)
    {
        if (string.IsNullOrWhiteSpace(filterQuery))
        {
            throw new ArgumentException("Jira filter query is required and cannot be empty.", nameof(filterQuery));
        }

        // Check for "in" or "not in" operators
        if (ContainsInOperator(filterQuery))
        {
            throw new ArgumentException(
                "Jira filter cannot use 'in' or 'not in' operators. Use 'project = VALUE' to filter by a single project.",
                nameof(filterQuery));
        }

        var projectCount = CountKeywordOccurrences(filterQuery, "project");
        if (projectCount != 1)
        {
            throw new ArgumentException(
                "Jira filter must contain 'project' keyword exactly once.",
                nameof(filterQuery));
        }
    }

    /// <summary>
    /// Validates that a Confluence filter query (CQL) contains the "space" keyword exactly once
    /// and does not use "in" or "not in" operators (which would allow multiple spaces).
    /// </summary>
    /// <param name="filterQuery">The CQL filter query to validate.</param>
    /// <exception cref="ArgumentException">
    /// Throws when filterQuery is null, empty, does not contain "space" exactly once,
    /// or uses "in" or "not in" operators.
    /// </exception>
    public static void ValidateConfluenceFilterQuery(string? filterQuery)
    {
        if (string.IsNullOrWhiteSpace(filterQuery))
        {
            throw new ArgumentException("Confluence filter query is required and cannot be empty.", nameof(filterQuery));
        }

        // Check for "in" or "not in" operators
        if (ContainsInOperator(filterQuery))
        {
            throw new ArgumentException(
                "Confluence filter cannot use 'in' or 'not in' operators. Use 'space = VALUE' to filter by a single space.",
                nameof(filterQuery));
        }

        var spaceCount = CountKeywordOccurrences(filterQuery, "space");
        if (spaceCount != 1)
        {
            throw new ArgumentException(
                "Confluence filter must contain 'space' keyword exactly once.",
                nameof(filterQuery));
        }
    }

    /// <summary>
    /// Detects if the query contains "in" or "not in" operators (case-insensitive).
    /// Uses word boundaries to avoid false matches on words like "assign", "domain", etc.
    /// </summary>
    private static bool ContainsInOperator(string text)
    {
        // Match "not in" or "in" with word boundaries (case-insensitive)
        var pattern = @"\b(not\s+)?in\b";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Counts occurrences of a keyword using word boundary regex (case-sensitive).
    /// Uses \b to ensure exact word matches (e.g., "project" matches but "projectId" does not).
    /// </summary>
    private static int CountKeywordOccurrences(string text, string keyword)
    {
        // Use \b for word boundaries to match exact keyword (case-sensitive)
        var pattern = $@"\b{Regex.Escape(keyword)}\b";
        var matches = Regex.Matches(text, pattern);
        return matches.Count;
    }
}
