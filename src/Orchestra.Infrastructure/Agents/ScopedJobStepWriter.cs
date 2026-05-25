using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents;

/// <summary>
/// Decorates <see cref="IJobStepWriter"/> and injects a fixed <c>parentStepId</c>,
/// <c>agentId</c>, and <c>agentName</c> into every write so that all steps emitted
/// during a sub-agent's execution are automatically linked to the parent call step.
/// </summary>
public class ScopedJobStepWriter : IJobStepWriter
{
    private readonly IJobStepWriter _inner;
    private readonly Guid _parentStepId;
    private readonly Guid _agentId;
    private readonly string _agentName;

    public ScopedJobStepWriter(
        IJobStepWriter inner,
        Guid parentStepId,
        Guid agentId,
        string agentName)
    {
        _inner = inner;
        _parentStepId = parentStepId;
        _agentId = agentId;
        _agentName = agentName;
    }

    public void InitializeSequence(int startingValue) => _inner.InitializeSequence(startingValue);

    public Task<Guid> WriteAsync(
        Guid jobId,
        Guid workspaceId,
        JobStepType stepType,
        string? content = null,
        string? toolName = null,
        bool isJson = false,
        long? durationMs = null,
        bool isError = false,
        Guid? parentStepId = null,
        Guid? agentId = null,
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        return _inner.WriteAsync(
            jobId,
            workspaceId,
            stepType,
            content,
            toolName,
            isJson,
            durationMs,
            isError,
            parentStepId ?? _parentStepId,
            agentId ?? _agentId,
            agentName ?? _agentName,
            cancellationToken);
    }
}
