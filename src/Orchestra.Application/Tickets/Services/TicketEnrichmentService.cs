using System.Text;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tickets.DTOs;
using Microsoft.Extensions.Logging;

namespace Orchestra.Application.Tickets.Services;

/// <summary>
/// Service implementation for ticket enrichment (sentiment analysis and summarization).
/// Extracted logic from TicketService.CalculateSentimentForTicketsAsync, 
/// CalculateSentimentForSingleTicketAsync, and GenerateSummaryAsync.
/// Code extracted exactly as-is with no behavioral changes.
/// </summary>
public class TicketEnrichmentService : ITicketEnrichmentService
{
    private readonly ISentimentAnalysisService _sentimentAnalysisService;
    private readonly ISummarizationService _summarizationService;
    private readonly ILogger<TicketEnrichmentService> _logger;

    public TicketEnrichmentService(
        ISentimentAnalysisService sentimentAnalysisService,
        ISummarizationService summarizationService,
        ILogger<TicketEnrichmentService> logger)
    {
        _sentimentAnalysisService = sentimentAnalysisService;
        _summarizationService = summarizationService;
        _logger = logger;
    }

    /// <summary>
    /// Calculates sentiment/satisfaction scores for a list of tickets.
    /// Modifies tickets in-place to populate Satisfaction field.
    /// Extracted logic from TicketService.CalculateSentimentForTicketsAsync (no changes).
    /// </summary>
    public async Task CalculateSentimentAsync(
        List<TicketDto> tickets,
        CancellationToken cancellationToken)
    {
        if (tickets == null || tickets.Count == 0)
            return;

        var ticketsToAnalyze = new List<TicketSentimentRequest>();
        var ticketIndexMap = new Dictionary<string, int>(); // Map ticketId to index in tickets list

        for (int i = 0; i < tickets.Count; i++)
        {
            var ticket = tickets[i];

            // Pure internal tickets always get 100
            if (ticket.Internal && ticket.IntegrationId == null)
            {
                tickets[i] = ticket with { Satisfaction = 100 };
                continue;
            }

            // Tickets without comments get 100
            if (ticket.Comments == null || ticket.Comments.Count == 0)
            {
                tickets[i] = ticket with { Satisfaction = 100 };
                continue;
            }

            // External tickets with comments need sentiment analysis
            var commentContents = ticket.Comments
                .Select(c => c.Content)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (commentContents.Count > 0)
            {
                ticketsToAnalyze.Add(new TicketSentimentRequest(
                    ticket.WorkspaceId,
                    ticket.Id,
                    commentContents
                ));
                ticketIndexMap[ticket.Id] = i;
            }
            else
            {
                // No valid comment content
                tickets[i] = ticket with { Satisfaction = 100 };
            }
        }

        // Analyze sentiment for external tickets with comments
        if (ticketsToAnalyze.Count > 0)
        {
            try
            {
                var sentimentResults = await _sentimentAnalysisService.AnalyzeBatchSentimentAsync(
                    ticketsToAnalyze,
                    cancellationToken);

                // Map results back to tickets
                foreach (var result in sentimentResults)
                {
                    if (ticketIndexMap.TryGetValue(result.TicketId, out var index))
                    {
                        var ticket = tickets[index];
                        tickets[index] = ticket with { Satisfaction = result.Sentiment };
                    }
                }

                _logger.LogInformation(
                    "Sentiment analysis complete for {Count} tickets",
                    sentimentResults.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze sentiment for tickets, defaulting to 100");
                
                // Default to 100 on error
                foreach (var ticketId in ticketIndexMap.Keys)
                {
                    if (ticketIndexMap.TryGetValue(ticketId, out var index))
                    {
                        var ticket = tickets[index];
                        tickets[index] = ticket with { Satisfaction = 100 };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Calculates sentiment/satisfaction score for a single ticket.
    /// Extracted logic from TicketService.CalculateSentimentForSingleTicketAsync (no changes).
    /// </summary>
    public async Task<TicketDto> CalculateSentimentForSingleAsync(
        TicketDto ticket,
        CancellationToken cancellationToken)
    {
        // Pure internal tickets always get 100
        if (ticket.Internal && ticket.IntegrationId == null)
        {
            return ticket with { Satisfaction = 100 };
        }

        // Tickets without comments get 100
        if (ticket.Comments == null || ticket.Comments.Count == 0)
        {
            return ticket with { Satisfaction = 100 };
        }

        // External tickets with comments need sentiment analysis
        var commentContents = ticket.Comments
            .Select(c => c.Content)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (commentContents.Count == 0)
        {
            return ticket with { Satisfaction = 100 };
        }

        try
        {
            var sentimentRequest = new TicketSentimentRequest(
                ticket.WorkspaceId,
                ticket.Id,
                commentContents
            );

            var sentimentResults = await _sentimentAnalysisService.AnalyzeBatchSentimentAsync(
                new List<TicketSentimentRequest> { sentimentRequest },
                cancellationToken);

            var result = sentimentResults.FirstOrDefault();
            if (result != null)
            {
                _logger.LogInformation(
                    "Sentiment analysis complete for ticket {TicketId}: {Sentiment}",
                    ticket.Id, result.Sentiment);
                
                return ticket with { Satisfaction = result.Sentiment };
            }

            // No result returned, default to 100
            return ticket with { Satisfaction = 100 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sentiment for ticket {TicketId}, defaulting to 100", ticket.Id);
            return ticket with { Satisfaction = 100 };
        }
    }

    /// <summary>
    /// Generates an AI summary of ticket content.
    /// The optional modelId parameter is forwarded unchanged to ISummarizationService,
    /// where it is resolved (availability check and fallback to default if needed).
    /// </summary>
    /// <param name="content">The pre-formatted ticket content to summarize.</param>
    /// <param name="modelId">
    /// Optional workspace-configured model identifier. Forwarded to ISummarizationService without validation.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated summary text.</returns>
    public async Task<string> GenerateSummaryAsync(string content, string? modelId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _summarizationService.GenerateSummaryAsync(content, modelId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to generate summary for content"); 
            throw;
        }
    }

    /// <summary>
    /// Builds formatted summary content from ticket details.
    /// Extracted and factored from TicketService.GenerateSummaryAsync (no behavioral changes).
    /// </summary>
    public string BuildSummaryContent(TicketDto ticket)
    {
        var contentBuilder = new StringBuilder();
        
        // Add title
        contentBuilder.AppendLine($"Title: {ticket.Title}");
        contentBuilder.AppendLine();
        
        // Add description
        contentBuilder.AppendLine("Description:");
        contentBuilder.AppendLine(ticket.Description);
        contentBuilder.AppendLine();
        
        // Add comments if any exist
        if (ticket.Comments != null && ticket.Comments.Any())
        {
            contentBuilder.AppendLine("Comments:");
            foreach (var comment in ticket.Comments)
            {
                contentBuilder.AppendLine($"- {comment.Author}: {comment.Content}");
            }
        }

        return contentBuilder.ToString();
    }
}
