namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for generating AI-powered summaries of ticket content.
/// </summary>
public interface ISummarizationService
{
    /// <summary>
    /// Generates a summary of the provided content using the AI provider configured for the given workspace.
    /// </summary>
    /// <param name="content">The content to summarize (ticket description + comments).</param>
    /// <param name="workspaceId">
    /// The workspace whose configured AI provider should be used to generate the summary.
    /// </param>
    /// <param name="modelId">
    /// The effective model identifier (never null). The caller must resolve the fallback chain
    /// (<c>AiSummarizationModelId ?? DefaultModelId ?? throw</c>) before calling this method.
    /// Forwarded directly to <see cref="IChatClientResolver.ResolveAsync"/> — no <c>ChatOptions</c> needed.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated summary text.</returns>
    /// <exception cref="SummarizationException">Thrown when summarization fails due to AI provider error.</exception>
    Task<string> GenerateSummaryAsync(
        string content,
        Guid workspaceId,
        string modelId,
        CancellationToken cancellationToken = default);
}
