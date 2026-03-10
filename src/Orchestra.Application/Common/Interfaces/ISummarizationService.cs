namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for generating AI-powered summaries of ticket content.
/// </summary>
public interface ISummarizationService
{
    /// <summary>
    /// Generates a summary of the provided content using AI.
    /// If a workspace-configured <paramref name="modelId"/> is provided and is currently available,
    /// that model is used. If modelId is null or the specified model is no longer available (stale),
    /// the service silently falls back to the startup-configured default model without raising an error.
    /// </summary>
    /// <param name="content">The content to summarize (ticket description + comments)</param>
    /// <param name="modelId">
    /// Optional workspace-configured model identifier. If null, the startup default is used.
    /// If non-null but unavailable (stale), the startup default is used silently.
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated summary text</returns>
    /// <exception cref="SummarizationException">Thrown when summarization fails due to AI provider error</exception>
    Task<string> GenerateSummaryAsync(string content, string? modelId = null, CancellationToken cancellationToken = default);
}
