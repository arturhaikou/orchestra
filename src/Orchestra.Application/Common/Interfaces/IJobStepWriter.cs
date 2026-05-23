using Orchestra.Domain.Enums;

namespace Orchestra.Application.Common.Interfaces;

public interface IJobStepWriter
{
    Task<Guid> WriteAsync(
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
        CancellationToken cancellationToken = default);
}
