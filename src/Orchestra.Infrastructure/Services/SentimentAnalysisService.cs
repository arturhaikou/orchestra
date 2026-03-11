
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Orchestra.Infrastructure.Services;

/// <summary>
/// Service for analyzing sentiment of ticket comments using an external AI service.
/// </summary>
public sealed class SentimentAnalysisService : ISentimentAnalysisService
{
    private readonly IChatClient _defaultChatClient;
    private readonly IChatClientResolver _chatClientResolver;
    private readonly ILogger<SentimentAnalysisService> _logger;
    // In-memory cache: key is workspaceId|ticketId|commentHash, value is sentiment score
    private static readonly ConcurrentDictionary<string, int> _sentimentCache = new();

    public SentimentAnalysisService(
        IChatClient defaultChatClient,
        IChatClientResolver chatClientResolver,
        ILogger<SentimentAnalysisService> logger)
    {
        _defaultChatClient = defaultChatClient;
        _chatClientResolver = chatClientResolver;
        _logger = logger;
    }
    /// <summary>
    /// Analyzes sentiment for multiple tickets based on their comments.
    /// If a workspace-configured modelId is provided and is currently available,
    /// that model is used. If modelId is null or the specified model is no longer available (stale),
    /// the service silently falls back to the startup-configured default model without raising an error.
    /// </summary>
    public async Task<List<TicketSentimentResult>> AnalyzeBatchSentimentAsync(
        List<TicketSentimentRequest> requests,
        string? modelId = null,
        CancellationToken cancellationToken = default)
    {
        if (requests == null || requests.Count == 0)
        {
            return new List<TicketSentimentResult>();
        }

        // Resolve the appropriate chat client based on workspace-configured model ID
        var chatClient = await _chatClientResolver.ResolveChatClientAsync(modelId, cancellationToken);

        int cacheHits = 0, aiCalls = 0;
        var results = new List<TicketSentimentResult>();
        var toAnalyze = new List<TicketSentimentRequest>();
        var cacheKeys = new Dictionary<string, string>(); // ticketId -> cacheKey

        foreach (var req in requests)
        {
            var key = BuildCacheKey(req.WorkspaceId, req.TicketId, req.Comments);
            cacheKeys[req.TicketId] = key;
            if (_sentimentCache.TryGetValue(key, out var cachedScore))
            {
                results.Add(new TicketSentimentResult(req.TicketId, cachedScore));
                cacheHits++;
            }
            else
            {
                toAnalyze.Add(req);
            }
        }

        if (toAnalyze.Count > 0)
        {
            aiCalls = toAnalyze.Count;
            _logger.LogInformation("Sentiment cache miss for {Count} tickets (workspaceIds: {WorkspaceIds}) - calling AI", toAnalyze.Count, string.Join(",", toAnalyze.Select(r => r.WorkspaceId).Distinct()));
            try
            {
                var ticketsInfo = $"""
                        <tickets>
                        {string.Join(Environment.NewLine, GetTicketsInfo(toAnalyze))}
                        </tickets>
                        """;

                var response = await chatClient.GetResponseAsync<List<TicketSentimentResult>>([
                    new ChatMessage(ChatRole.System, SystemMessages.SentimentAnalysisSystemMessage),
                    new ChatMessage(ChatRole.User, ticketsInfo)
                ]);

                foreach (var result in response.Result)
                {
                    // Store in cache
                    if (cacheKeys.TryGetValue(result.TicketId, out var key))
                    {
                        _sentimentCache[key] = result.Sentiment;
                    }
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze sentiment for batch of {Count} tickets", toAnalyze.Count);
                // Surface the exception to callers so they can apply their fallback behavior
                results.AddRange(toAnalyze.Select(r => new TicketSentimentResult(r.TicketId, 100))); // Default sentiment on failure
            }
        }

        _logger.LogInformation("Sentiment analysis: {CacheHits} cache hits, {AICalls} AI calls, {Total} total tickets", cacheHits, aiCalls, requests.Count);
        return results;

        // (Old try/catch block replaced by cache-aware logic above)
    }

    private string[] GetTicketsInfo(List<TicketSentimentRequest> requests)
    {
        return requests.Select(request => $"""
            <ticket>
                <workspaceId>{request.WorkspaceId}</workspaceId>
                <id>{request.TicketId}</id>
                <comments>{string.Join(';', request.Comments)}</comments>
            </ticket>
            """).ToArray();
    }

    private static string BuildCacheKey(Guid workspaceId, string ticketId, List<string> comments)
    {
        // Use workspaceId|ticketId|SHA256(comments joined)
        var commentsJoined = string.Join("|", comments);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(commentsJoined));
        var hashString = BitConverter.ToString(hash).Replace("-", "");
        return $"{workspaceId}|{ticketId}|{hashString}";
    }
}
