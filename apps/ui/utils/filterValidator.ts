export interface FilterValidationResult {
  isValid: boolean;
  error?: string;
}

/**
 * Validates a filter query for a given provider.
 * - Jira (TRACKER): Filter must contain "project" exactly once and cannot use "in"/"not in" operators
 * - Confluence (TRACKER): Filter must contain "space" exactly once and cannot use "in"/"not in" operators
 * - Other providers: No validation (always valid)
 * 
 * @param filterQuery The filter query string to validate
 * @param provider The integration provider ('jira', 'confluence', etc.)
 * @returns Object with isValid flag and optional error message
 */
export function validateFilterQuery(
  filterQuery: string | undefined | null,
  provider: string
): FilterValidationResult {
  // No filter query provided
  if (!filterQuery || filterQuery.trim() === '') {
    // Jira and Confluence require filter queries
    if (provider.toLowerCase() === 'jira') {
      return {
        isValid: false,
        error: 'Jira filter query is required and cannot be empty.',
      };
    }
    if (provider.toLowerCase() === 'confluence') {
      return {
        isValid: false,
        error: 'Confluence filter query is required and cannot be empty.',
      };
    }
    // Other providers: filter is optional
    return { isValid: true };
  }

  // Check for "in" or "not in" operators (case-insensitive)
  if (containsInOperator(filterQuery)) {
    if (provider.toLowerCase() === 'jira') {
      return {
        isValid: false,
        error: "Jira filter cannot use 'in' or 'not in' operators. Use 'project = VALUE' to filter by a single project.",
      };
    }
    if (provider.toLowerCase() === 'confluence') {
      return {
        isValid: false,
        error: "Confluence filter cannot use 'in' or 'not in' operators. Use 'space = VALUE' to filter by a single space.",
      };
    }
  }

  // Validate Jira filter
  if (provider.toLowerCase() === 'jira') {
    const projectCount = countKeywordOccurrences(filterQuery, 'project');
    if (projectCount !== 1) {
      return {
        isValid: false,
        error: "Jira filter must contain 'project' keyword exactly once.",
      };
    }
  }

  // Validate Confluence filter
  if (provider.toLowerCase() === 'confluence') {
    const spaceCount = countKeywordOccurrences(filterQuery, 'space');
    if (spaceCount !== 1) {
      return {
        isValid: false,
        error: "Confluence filter must contain 'space' keyword exactly once.",
      };
    }
  }

  return { isValid: true };
}

/**
 * Detects if the query contains "in" or "not in" operators (case-insensitive).
 * Uses word boundaries to avoid false matches on words like "assign", "domain", etc.
 * 
 * @param text The text to search in
 * @returns True if "in" or "not in" operators are found
 */
function containsInOperator(text: string): boolean {
  // Match "not in" or "in" with word boundaries (case-insensitive)
  const pattern = /\b(?:not\s+)?in\b/i;
  return pattern.test(text);
}

/**
 * Counts occurrences of a keyword using word boundary regex (case-sensitive).
 * Uses \b to ensure exact word matches (e.g., "project" matches but "projectId" does not).
 * 
 * @param text The text to search in
 * @param keyword The keyword to count
 * @returns Number of exact keyword matches
 */
function countKeywordOccurrences(text: string, keyword: string): number {
  // Use \b for word boundaries to match exact keyword (case-sensitive)
  const pattern = new RegExp(`\\b${keyword}\\b`, 'g');
  const matches = text.match(pattern);
  return matches ? matches.length : 0;
}
