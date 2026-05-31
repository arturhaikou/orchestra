namespace Orchestra.Domain.Entities;

public class WorkflowDefinition
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private WorkflowDefinition() { }

    public static WorkflowDefinition Create(Guid workspaceId, string name, string? description)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID is required.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        return new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }
}
