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

    public JobFunctionCallingMiddlewareHandler(IJobStepWriter stepWriter, Guid jobId, Guid workspaceId)
    {
        _stepWriter = stepWriter;
        _jobId = jobId;
        _workspaceId = workspaceId;
    }

    public async ValueTask<object?> HandleAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
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
