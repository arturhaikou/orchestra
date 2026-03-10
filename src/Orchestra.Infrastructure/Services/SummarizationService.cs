using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Orchestra.Infrastructure.Services;

public class SummarizationService(
    IChatClientResolver _chatClientResolver,
    ILogger<SummarizationService> _logger) : ISummarizationService
{
    public async Task<string> GenerateSummaryAsync(string content, string? modelId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve the appropriate IChatClient based on the workspace-configured model ID
            var chatClient = await _chatClientResolver.ResolveChatClientAsync(modelId, cancellationToken);
            
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, SystemMessages.SummarizationSystemMessage),
                    new ChatMessage(ChatRole.User, content)
                ],
                cancellationToken: cancellationToken);
            
            return response.Text;
        }
        catch (Exception ex) when (ex is not SummarizationException)
        {
            throw new SummarizationException("Failed to generate summary", ex);
        }
    }
}
