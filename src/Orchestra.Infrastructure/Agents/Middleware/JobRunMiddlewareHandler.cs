using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents.Middleware;

public class JobRunMiddlewareHandler
{
    private readonly IJobStepWriter _stepWriter;
    private readonly Guid _jobId;
    private readonly Guid _workspaceId;

    public JobRunMiddlewareHandler(IJobStepWriter stepWriter, Guid jobId, Guid workspaceId)
    {
        _stepWriter = stepWriter;
        _jobId = jobId;
        _workspaceId = workspaceId;
    }

    public async Task<AgentResponse> HandleAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        await _stepWriter.WriteAsync(
            _jobId,
            _workspaceId,
            JobStepType.AgentStarted,
            cancellationToken: cancellationToken);

        try
        {
            var response = await innerAgent.RunAsync(
                messages,
                session,
                options,
                cancellationToken);

            await _stepWriter.WriteAsync(
                _jobId,
                _workspaceId,
                JobStepType.AgentCompleted,
                content: response.Text,
                cancellationToken: cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            await _stepWriter.WriteAsync(
                _jobId,
                _workspaceId,
                JobStepType.AgentFailed,
                content: ex.Message,
                isError: true,
                cancellationToken: cancellationToken);
            throw;
        }
    }
}
