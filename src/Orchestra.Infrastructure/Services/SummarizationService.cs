using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common;
using Microsoft.Extensions.AI;

namespace Orchestra.Infrastructure.Services;

public class SummarizationService(IChatClient _chatClient) : ISummarizationService
{
    public async Task<string> GenerateSummaryAsync(string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _chatClient.GetResponseAsync([new ChatMessage(ChatRole.System, SystemMessages.SummarizationSystemMessage), new ChatMessage(ChatRole.User, content)]);
            return response.Text;
        }
        catch (Exception ex) when (ex is not SummarizationException)
        {
            throw new SummarizationException("Failed to generate summary", ex);
        }
    }
}
