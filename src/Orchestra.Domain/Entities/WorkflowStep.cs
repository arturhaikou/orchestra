namespace Orchestra.Domain.Entities;

public class WorkflowStep
{
    public Guid Id { get; private set; }
    public Guid WorkflowDefinitionId { get; private set; }
    public int Order { get; private set; }
    public Guid AgentId { get; private set; }
    public string? InstructionOverride { get; private set; }
    public bool PassPreviousOutput { get; private set; }

    private WorkflowStep() { }

    public static WorkflowStep Create(
        Guid workflowDefinitionId,
        int order,
        Guid agentId,
        string? instructionOverride,
        bool passPreviousOutput)
    {
        if (workflowDefinitionId == Guid.Empty)
            throw new ArgumentException("WorkflowDefinition ID is required.", nameof(workflowDefinitionId));

        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent ID is required.", nameof(agentId));

        return new WorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = workflowDefinitionId,
            Order = order,
            AgentId = agentId,
            InstructionOverride = instructionOverride,
            PassPreviousOutput = passPreviousOutput
        };
    }

    public void Update(int order, Guid agentId, string? instructionOverride, bool passPreviousOutput)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent ID is required.", nameof(agentId));

        Order = order;
        AgentId = agentId;
        InstructionOverride = instructionOverride;
        PassPreviousOutput = passPreviousOutput;
    }
}
