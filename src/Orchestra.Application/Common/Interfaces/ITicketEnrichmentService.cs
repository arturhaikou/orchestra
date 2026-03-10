namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for enriching tickets with AI-generated content (sentiment analysis and summarization).
/// Calculates satisfaction scores based on sentiment and generates content summaries.
/// </summary>
public interface ITicketEnrichmentService
{
    /// <summary>
    /// Calculates sentiment/satisfaction scores for a list of tickets.
    /// - Internal tickets always get Satisfaction = 100
    /// - Tickets without comments get Satisfaction = 100
    /// - External tickets with comments are analyzed by the sentiment service
    /// </summary>
    /// <param name="tickets">List of tickets to enrich (modified in-place)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CalculateSentimentAsync(List<Orchestra.Application.Tickets.DTOs.TicketDto> tickets, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates sentiment/satisfaction score for a single ticket.
    /// Mirrors CalculateSentimentAsync logic for individual ticket processing.
    /// </summary>
    /// <param name="ticket">The ticket to enrich</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ticket with Satisfaction score populated</returns>
    Task<Orchestra.Application.Tickets.DTOs.TicketDto> CalculateSentimentForSingleAsync(
        Orchestra.Application.Tickets.DTOs.TicketDto ticket,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an AI summary of ticket content (title + description + comments).
    /// The optional modelId parameter is the workspace-configured model preference and is forwarded
    /// unchanged to the underlying ISummarizationService. Model resolution (availability check and fallback)
    /// is the responsibility of ISummarizationService, not this layer.
    /// </summary>
    /// <param name="content">The content to summarize (pre-formatted ticket content)</param>
    /// <param name="modelId">
    /// Optional workspace-configured model identifier. Forwarded unchanged to ISummarizationService.
    /// If null, the service will use its startup-configured default model.
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated summary text</returns>
    /// <exception cref="Exception">Thrown when summarization fails</exception>
    Task<string> GenerateSummaryAsync(string content, string? modelId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a formatted summary content string from ticket details.
    /// Format: Title + Description + Comments list
    /// </summary>
    /// <param name="ticket">The ticket to build content from</param>
    /// <returns>Formatted content string ready for summarization</returns>
    string BuildSummaryContent(Orchestra.Application.Tickets.DTOs.TicketDto ticket);
}
