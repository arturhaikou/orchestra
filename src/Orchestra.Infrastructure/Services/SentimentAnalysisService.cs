using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Service for analyzing sentiment of ticket comments using an external AI service.
/// </summary>
public class SentimentAnalysisService(IChatClient _chatClient, ILogger<SentimentAnalysisService> _logger) : ISentimentAnalysisService
{
    /// <summary>
    /// Analyzes sentiment for multiple tickets based on their comments.
    /// </summary>
    public async Task<List<TicketSentimentResult>> AnalyzeBatchSentimentAsync(
        List<TicketSentimentRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests == null || requests.Count == 0)
        {
            return new List<TicketSentimentResult>();
        }

        _logger.LogInformation("Analyzing sentiment for {Count} tickets", requests.Count);

        try
        {
            var ticketsInfo = $"""
                    <tickets>
                    {string.Join(Environment.NewLine, GetTicketsInfo(requests))}
                    </tickets>
                    """;

            var response = await _chatClient.GetResponseAsync<List<TicketSentimentResult>>([new ChatMessage(ChatRole.System, SystemMessages.SentimentAnalysisSystemMessage), new ChatMessage(ChatRole.User, ticketsInfo)]);

            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze sentiment for batch of {Count} tickets", requests.Count);
            
            // Return default sentiment scores on error
            return requests.Select(r => new TicketSentimentResult(r.TicketId, 75)).ToList();
        }
    }

    private string[] GetTicketsInfo(List<TicketSentimentRequest> requests)
    {
        return requests.Select(request => $"""
            <ticket>
                <id>{request.TicketId}</title>
                <comments>{string.Join(';', request.Comments)}</comments>
            </ticket>
            """).ToArray();
    }
}
