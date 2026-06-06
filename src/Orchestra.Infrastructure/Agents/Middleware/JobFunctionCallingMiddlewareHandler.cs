using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents.Middleware;

public class JobFunctionCallingMiddlewareHandler
{
    private readonly IJobStepWriter _stepWriter;
    private readonly Guid _jobId;
    private readonly Guid _workspaceId;
    private readonly CancellationToken _jobCancellationToken;

    public JobFunctionCallingMiddlewareHandler(
        IJobStepWriter stepWriter,
        Guid jobId,
        Guid workspaceId,
        CancellationToken jobCancellationToken = default)
    {
        _stepWriter = stepWriter;
        _jobId = jobId;
        _workspaceId = workspaceId;
        _jobCancellationToken = jobCancellationToken;
    }

    public async ValueTask<object?> HandleAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        _jobCancellationToken.ThrowIfCancellationRequested();

        var inputJson = JsonSerializer.Serialize(context.Arguments);
        await _stepWriter.WriteAsync(
            _jobId,
            _workspaceId,
            JobStepType.ToolCallStarted,
            toolName: context.Function.Name,
            content: inputJson,
            isJson: true,
            cancellationToken: cancellationToken);

        var sw = Stopwatch.StartNew();
        var result = await next(context, cancellationToken);
        sw.Stop();

        _jobCancellationToken.ThrowIfCancellationRequested();

        var outputJson = JsonSerializer.Serialize(result);
        await _stepWriter.WriteAsync(
            _jobId,
            _workspaceId,
            JobStepType.ToolCallCompleted,
            toolName: context.Function.Name,
            content: outputJson,
            isJson: true,
            durationMs: sw.ElapsedMilliseconds,
            cancellationToken: cancellationToken);

        return result;
    }
}
