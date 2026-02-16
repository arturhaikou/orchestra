using System.Collections.Generic;
using System.Text.Json;
using System.Threading;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for converting Atlassian Document Format (ADF) to Markdown.
/// </summary>
public interface IAdfConversionService
{
    /// <summary>
    /// Converts a single ADF document to Markdown format.
    /// </summary>
    /// <param name="adf">The ADF document as a JSON element.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// The converted Markdown string.
    /// </returns>
    /// <remarks>
    /// Used for converting ticket descriptions. Throws exception on conversion failure to fail the sync operation per user preference for data consistency.
    /// </remarks>
    Task<string> ConvertAdfToMarkdownAsync(JsonElement adf, CancellationToken cancellationToken);

    /// <summary>
    /// Converts a batch of ADF documents to Markdown format for optimization.
    /// </summary>
    /// <param name="adfList">List of ADF documents as JSON elements.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// List of converted Markdown strings in the same order as input.
    /// </returns>
    /// <remarks>
    /// Used for converting multiple comments efficiently. Throws exception on any conversion failure to maintain data consistency.
    /// </remarks>
    Task<IReadOnlyList<string>> ConvertAdfBatchToMarkdownAsync(IReadOnlyList<JsonElement> adfList, CancellationToken cancellationToken);

    /// <summary>
    /// Converts Markdown text to Atlassian Document Format (ADF).
    /// </summary>
    /// <param name="markdown">The Markdown text to convert.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// The converted ADF document as a JSON element.
    /// </returns>
    /// <remarks>
    /// Used for converting user-provided markdown content (e.g., comments) to ADF format before sending to external systems like Jira.
    /// </remarks>
    Task<JsonElement> ConvertMarkdownToAdfAsync(string markdown, CancellationToken cancellationToken);
}