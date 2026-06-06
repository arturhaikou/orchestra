using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

public class WorkflowStep
{
    public Guid Id { get; private set; }
    public Guid WorkflowDefinitionId { get; private set; }
    public int Order { get; private set; }
    public Guid? AgentId { get; private set; }
    public string? InstructionOverride { get; private set; }
    public bool PassPreviousOutput { get; private set; }
    public WorkflowStepType StepType { get; private set; } = WorkflowStepType.Agent;
    public string? Condition { get; private set; }
    public Guid? TrueNextStepId { get; private set; }
    public Guid? FalseNextStepId { get; private set; }

    private WorkflowStep() { }

    public static WorkflowStep Create(
        Guid id,
        Guid workflowDefinitionId,
        int order,
        Guid? agentId,
        string? instructionOverride,
        bool passPreviousOutput,
        WorkflowStepType stepType = WorkflowStepType.Agent,
        string? condition = null,
        Guid? trueNextStepId = null,
        Guid? falseNextStepId = null)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw new ArgumentException("WorkflowDefinition ID is required.", nameof(workflowDefinitionId));

        if (stepType == WorkflowStepType.Agent && (agentId == null || agentId == Guid.Empty))
            throw new ArgumentException("Agent ID is required for Agent steps.", nameof(agentId));

        return new WorkflowStep
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            WorkflowDefinitionId = workflowDefinitionId,
            Order = order,
            AgentId = agentId,
            InstructionOverride = instructionOverride,
            PassPreviousOutput = passPreviousOutput,
            StepType = stepType,
            Condition = condition,
            TrueNextStepId = trueNextStepId,
            FalseNextStepId = falseNextStepId
        };
    }

    public void Update(
        int order,
        Guid? agentId,
        string? instructionOverride,
        bool passPreviousOutput,
        WorkflowStepType stepType = WorkflowStepType.Agent,
        string? condition = null,
        Guid? trueNextStepId = null,
        Guid? falseNextStepId = null)
    {
        if (stepType == WorkflowStepType.Agent && (agentId == null || agentId == Guid.Empty))
            throw new ArgumentException("Agent ID is required for Agent steps.", nameof(agentId));

        Order = order;
        AgentId = agentId;
        InstructionOverride = instructionOverride;
        PassPreviousOutput = passPreviousOutput;
        StepType = stepType;
        Condition = condition;
        TrueNextStepId = trueNextStepId;
        FalseNextStepId = falseNextStepId;
    }
}
