namespace Orchestra.Domain.Entities;

public class WorkflowStepSystemTool
{
    public Guid Id { get; private set; }
    public Guid WorkflowStepId { get; private set; }
    public string ToolIdentifier { get; private set; } = default!;

    private WorkflowStepSystemTool() { }

    public static WorkflowStepSystemTool Create(Guid workflowStepId, string toolIdentifier)
    {
        if (workflowStepId == Guid.Empty)
            throw new ArgumentException("WorkflowStep ID is required.", nameof(workflowStepId));
        if (string.IsNullOrWhiteSpace(toolIdentifier))
            throw new ArgumentException("Tool identifier is required.", nameof(toolIdentifier));

        return new WorkflowStepSystemTool
        {
            Id = Guid.NewGuid(),
            WorkflowStepId = workflowStepId,
            ToolIdentifier = toolIdentifier
        };
    }
}
