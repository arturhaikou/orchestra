using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Application.Jobs.DTOs;

public record JobTrackingContext(
    IJobStepWriter StepWriter,
    Guid JobId,
    Guid WorkspaceId);
