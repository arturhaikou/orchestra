using Microsoft.Extensions.AI;
using Orchestra.Application.Jobs.DTOs;

namespace Orchestra.Application.Workflows.Interfaces;

public interface IWorkflowSystemToolRegistry
{
    IReadOnlyList<string> AvailableTools { get; }
    AIFunction? Create(string toolIdentifier, JobTrackingContext jobTracking);
}
