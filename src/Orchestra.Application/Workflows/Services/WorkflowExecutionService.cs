using Orchestra.Application.Workflows.DTOs;
using Orchestra.Application.Workflows.Interfaces;

namespace Orchestra.Application.Workflows.Services;

public class WorkflowExecutionService : IWorkflowExecutionService
{
    private readonly IWorkflowExecutionRepository _repository;

    public WorkflowExecutionService(IWorkflowExecutionRepository repository)
    {
        _repository = repository;
    }

    public async Task<WorkflowExecutionDto?> GetByIdAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await _repository.GetByIdAsync(executionId, cancellationToken);
        if (execution is null) return null;

        var stepExecutions = await _repository.GetStepExecutionsByExecutionIdAsync(executionId, cancellationToken);
        return ToDto(execution, stepExecutions);
    }

    public async Task<List<WorkflowExecutionDto>> GetByTicketIdAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var executions = await _repository.GetByTicketIdAsync(ticketId, cancellationToken);

        var result = new List<WorkflowExecutionDto>();
        foreach (var execution in executions)
        {
            var stepExecutions = await _repository.GetStepExecutionsByExecutionIdAsync(execution.Id, cancellationToken);
            result.Add(ToDto(execution, stepExecutions));
        }

        return result;
    }

    private static WorkflowExecutionDto ToDto(
        Domain.Entities.WorkflowExecution execution,
        List<Domain.Entities.WorkflowStepExecution> stepExecutions)
    {
        var stepDtos = stepExecutions.Select(s => new WorkflowStepExecutionDto(
            s.Id,
            s.WorkflowExecutionId,
            s.StepIndex,
            s.JobId,
            s.Status,
            s.StartedAt,
            s.CompletedAt,
            s.Output)).ToList();

        return new WorkflowExecutionDto(
            execution.Id,
            execution.WorkflowDefinitionId,
            execution.TicketId,
            execution.WorkspaceId,
            execution.Status,
            execution.CurrentStepIndex,
            execution.StartedAt,
            execution.CompletedAt,
            stepDtos,
            execution.WorkflowJobId);
    }
}
