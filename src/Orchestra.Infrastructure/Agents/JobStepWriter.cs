using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Jobs.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Agents;

public class JobStepWriter : IJobStepWriter
{
    private readonly IJobDataAccess _jobDataAccess;
    private readonly INotificationService _notificationService;
    private int _sequenceCounter;

    public JobStepWriter(IJobDataAccess jobDataAccess, INotificationService notificationService)
    {
        _jobDataAccess = jobDataAccess;
        _notificationService = notificationService;
    }

    public void InitializeSequence(int startingValue) =>
        Interlocked.Exchange(ref _sequenceCounter, startingValue);

    public async Task<Guid> WriteAsync(
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
        var sequence = Interlocked.Increment(ref _sequenceCounter);

        var step = JobStep.Create(jobId, stepType, sequence, content, isJson, toolName, durationMs, isError, parentStepId, agentId, agentName);
        await _jobDataAccess.AddStepAsync(step, cancellationToken);

        var stepDto = new JobStepDto(
            step.Id,
            step.StepType,
            step.Sequence,
            step.Timestamp,
            step.Content,
            step.ToolName,
            step.IsJson,
            step.DurationMs,
            step.IsError,
            step.ParentStepId,
            step.AgentId,
            step.AgentName);

        await _notificationService.NotifyJobStepAddedAsync(workspaceId, jobId, stepDto, cancellationToken);

        return step.Id;
    }
}
