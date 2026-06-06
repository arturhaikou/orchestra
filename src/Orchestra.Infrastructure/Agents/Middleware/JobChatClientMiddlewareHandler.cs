using Microsoft.Extensions.AI;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents.Middleware;

public class JobChatClientMiddlewareHandler
{
    private readonly IJobStepWriter _stepWriter;
    private readonly Guid _jobId;
    private readonly Guid _workspaceId;

    public JobChatClientMiddlewareHandler(IJobStepWriter stepWriter, Guid jobId, Guid workspaceId)
    {
        _stepWriter = stepWriter;
        _jobId = jobId;
        _workspaceId = workspaceId;
    }

    public async Task<ChatResponse> HandleAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = await innerChatClient.GetResponseAsync(
            messages,
            options,
            cancellationToken);

        var assistantMessages = response.Messages
            .Where(m => m.Role == ChatRole.Assistant && !string.IsNullOrEmpty(m.Text));

        foreach (var msg in assistantMessages)
        {
            await _stepWriter.WriteAsync(
                _jobId,
                _workspaceId,
                JobStepType.ThinkingMessage,
                content: msg.Text,
                cancellationToken: cancellationToken);
        }

        return response;
    }
}
