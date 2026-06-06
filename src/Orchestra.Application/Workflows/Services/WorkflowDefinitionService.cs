using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Workflows.DTOs;
using Orchestra.Application.Workflows.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Workflows.Services;

public class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;

    public WorkflowDefinitionService(
        IWorkflowDefinitionRepository repository,
        IAgentDataAccess agentDataAccess,
        IWorkspaceAuthorizationService workspaceAuthorizationService)
    {
        _repository = repository;
        _agentDataAccess = agentDataAccess;
        _workspaceAuthorizationService = workspaceAuthorizationService;
    }

    public async Task<WorkflowDefinitionDto> CreateAsync(
        Guid userId,
        CreateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, request.WorkspaceId, cancellationToken);

        var definition = WorkflowDefinition.Create(request.WorkspaceId, request.Name, request.Description);
        await _repository.AddAsync(definition, cancellationToken);

        var steps = BuildSteps(definition.Id, request.Steps);
        await _repository.ReplaceStepsAsync(definition.Id, steps, cancellationToken);
        await SaveStepSystemToolsAsync(steps, request.Steps, cancellationToken);

        var systemToolsMap = await _repository.GetSystemToolsByDefinitionIdAsync(definition.Id, cancellationToken);
        return await BuildDtoAsync(definition, steps, systemToolsMap, cancellationToken);
    }

    public async Task<WorkflowDefinitionDto> UpdateAsync(
        Guid userId,
        Guid workflowId,
        UpdateWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = await _repository.GetByIdAsync(workflowId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found.");

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, definition.WorkspaceId, cancellationToken);

        definition.Update(request.Name, request.Description);
        await _repository.UpdateAsync(definition, cancellationToken);

        var steps = BuildSteps(definition.Id, request.Steps);
        await _repository.ReplaceStepsAsync(definition.Id, steps, cancellationToken);
        await SaveStepSystemToolsAsync(steps, request.Steps, cancellationToken);

        var systemToolsMap = await _repository.GetSystemToolsByDefinitionIdAsync(definition.Id, cancellationToken);
        return await BuildDtoAsync(definition, steps, systemToolsMap, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var definition = await _repository.GetByIdAsync(workflowId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow {workflowId} not found.");

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, definition.WorkspaceId, cancellationToken);

        await _repository.DeleteAsync(workflowId, cancellationToken);
    }

    public async Task<WorkflowDefinitionDto?> GetByIdAsync(
        Guid userId,
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        var definition = await _repository.GetByIdAsync(workflowId, cancellationToken);
        if (definition is null) return null;

        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, definition.WorkspaceId, cancellationToken);

        var steps = await _repository.GetStepsByDefinitionIdAsync(workflowId, cancellationToken);
        var systemToolsMap = await _repository.GetSystemToolsByDefinitionIdAsync(workflowId, cancellationToken);
        return await BuildDtoAsync(definition, steps, systemToolsMap, cancellationToken);
    }

    public async Task<List<WorkflowDefinitionDto>> GetByWorkspaceAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAuthorizationService.EnsureUserIsMemberAsync(userId, workspaceId, cancellationToken);

        var definitions = await _repository.GetByWorkspaceAsync(workspaceId, cancellationToken);

        var results = new List<WorkflowDefinitionDto>();
        foreach (var definition in definitions)
        {
            var steps = await _repository.GetStepsByDefinitionIdAsync(definition.Id, cancellationToken);
            var systemToolsMap = await _repository.GetSystemToolsByDefinitionIdAsync(definition.Id, cancellationToken);
            results.Add(await BuildDtoAsync(definition, steps, systemToolsMap, cancellationToken));
        }

        return results;
    }

    private static List<WorkflowStep> BuildSteps(Guid definitionId, List<CreateWorkflowStepRequest> requests)
    {
        // Build a clientId → Guid map so cross-references can be resolved as real step IDs.
        var clientIdToGuid = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in requests)
        {
            if (!string.IsNullOrWhiteSpace(r.ClientId))
                clientIdToGuid[r.ClientId] = Guid.NewGuid();
        }

        return requests.Select(r =>
        {
            var stepId = !string.IsNullOrWhiteSpace(r.ClientId) && clientIdToGuid.TryGetValue(r.ClientId, out var existingId)
                ? existingId
                : Guid.NewGuid();

            var stepType = string.Equals(r.Type, "Condition", StringComparison.OrdinalIgnoreCase)
                ? WorkflowStepType.Condition
                : WorkflowStepType.Agent;

            Guid? trueNextStepId = null;
            if (!string.IsNullOrWhiteSpace(r.TrueNextClientId) && clientIdToGuid.TryGetValue(r.TrueNextClientId, out var trueId))
                trueNextStepId = trueId;

            Guid? falseNextStepId = null;
            if (!string.IsNullOrWhiteSpace(r.FalseNextClientId) && clientIdToGuid.TryGetValue(r.FalseNextClientId, out var falseId))
                falseNextStepId = falseId;

            return WorkflowStep.Create(
                stepId,
                definitionId,
                r.Order,
                r.AgentId,
                r.InstructionOverride,
                r.PassPreviousOutput,
                stepType,
                r.Condition,
                trueNextStepId,
                falseNextStepId);
        }).ToList();
    }

    private async Task SaveStepSystemToolsAsync(
        List<WorkflowStep> steps,
        List<CreateWorkflowStepRequest> requests,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var tools = requests[i].SystemTools ?? [];
            await _repository.ReplaceStepSystemToolsAsync(steps[i].Id, tools, cancellationToken);
        }
    }

    private async Task<WorkflowDefinitionDto> BuildDtoAsync(
        WorkflowDefinition definition,
        List<WorkflowStep> steps,
        Dictionary<Guid, List<string>> systemToolsMap,
        CancellationToken cancellationToken)
    {
        var agentIds = steps
            .Where(s => s.AgentId.HasValue)
            .Select(s => s.AgentId!.Value)
            .Distinct()
            .ToList();
        var agents = await LoadAgentsAsync(agentIds, cancellationToken);

        var stepDtos = steps.Select(s => new WorkflowStepDto(
            s.Id,
            s.WorkflowDefinitionId,
            s.Order,
            s.AgentId,
            s.AgentId.HasValue && agents.TryGetValue(s.AgentId.Value, out var a) ? a.Name : "Condition",
            s.InstructionOverride,
            s.PassPreviousOutput,
            systemToolsMap.GetValueOrDefault(s.Id, []),
            s.StepType.ToString(),
            s.Condition,
            s.TrueNextStepId,
            s.FalseNextStepId)).ToList();

        return new WorkflowDefinitionDto(
            definition.Id,
            definition.WorkspaceId,
            definition.Name,
            definition.Description,
            stepDtos,
            definition.CreatedAt,
            definition.UpdatedAt);
    }

    private async Task<Dictionary<Guid, Agent>> LoadAgentsAsync(
        IEnumerable<Guid> agentIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Agent>();
        foreach (var agentId in agentIds)
        {
            var agent = await _agentDataAccess.GetByIdAsync(agentId, cancellationToken);
            if (agent is not null)
                result[agentId] = agent;
        }
        return result;
    }
}
