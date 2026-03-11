namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Request model for sentiment analysis containing ticket ID and associated comments.
/// </summary>
/// <param name="WorkspaceId">The unique identifier of the workspace.</param>
/// <param name="TicketId">The unique identifier of the ticket.</param>
/// <param name="Comments">List of comment content strings to analyze.</param>
public record TicketSentimentRequest(
    Guid WorkspaceId,
    string TicketId,
    List<string> Comments
);

/// <summary>
/// Result model containing the calculated sentiment score for a ticket.
/// </summary>
/// <param name="TicketId">The unique identifier of the ticket.</param>
/// <param name="Sentiment">Sentiment score ranging from 0 (most negative) to 100 (most positive).</param>
public record TicketSentimentResult(
    string TicketId,
    int Sentiment
);

/// <summary>
/// Service for analyzing sentiment of ticket comments using an external AI service.
/// </summary>
public interface ISentimentAnalysisService
{
    /// <summary>
    /// Analyzes sentiment for multiple tickets based on their comments.
    /// If a workspace-configured <paramref name="modelId"/> is provided and is currently available,
    /// that model is used. If modelId is null or the specified model is no longer available (stale),
    /// the service silently falls back to the startup-configured default model without raising an error.
    /// </summary>
    /// <param name="requests">List of tickets with their workspace IDs and comments to analyze.</param>
    /// <param name="modelId">
    /// Optional workspace-configured model identifier. If null, the startup default is used.
    /// If non-null but unavailable (stale), the startup default is used silently.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sentiment analysis results mapped to ticket IDs.</returns>
    Task<List<TicketSentimentResult>> AnalyzeBatchSentimentAsync(
        List<TicketSentimentRequest> requests,
        string? modelId = null,
        CancellationToken cancellationToken = default
    );
}
