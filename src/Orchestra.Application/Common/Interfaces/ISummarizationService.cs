namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for generating AI-powered summaries of ticket content.
/// </summary>
public interface ISummarizationService
{
    /// <summary>
    /// Generates a summary of the provided content using AI.
    /// </summary>
    /// <param name="content">The content to summarize (ticket description + comments)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated summary text</returns>
    /// <exception cref="SummarizationException">Thrown when summarization fails</exception>
    Task<string> GenerateSummaryAsync(string content, CancellationToken cancellationToken = default);
}
