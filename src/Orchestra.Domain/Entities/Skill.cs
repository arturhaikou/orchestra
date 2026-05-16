namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents a reusable skill that packages domain expertise and instructions
/// for agents within a workspace. Skills follow the Agent Skills specification and
/// use a progressive disclosure pattern when injected into agent runtime contexts.
/// </summary>
public class Skill
{
    /// <summary>
    /// Gets the unique identifier of the skill.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the workspace identifier this skill belongs to.
    /// </summary>
    public Guid WorkspaceId { get; private set; }

    /// <summary>
    /// Gets the slug-style name of the skill.
    /// Max 64 characters; lowercase letters, numbers, and hyphens only.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the human-readable description of the skill.
    /// Advertised to the agent in the system prompt so it knows when to invoke this skill.
    /// Max 1024 characters.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the full skill instructions loaded on demand by the agent.
    /// Contains step-by-step guidance, examples, and domain knowledge.
    /// Max 5000 characters.
    /// </summary>
    public string Instructions { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp when the skill was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the skill was last updated, or null if never updated.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private Skill() { }

    /// <summary>
    /// Creates a new <see cref="Skill"/> with validation.
    /// </summary>
    /// <param name="workspaceId">The workspace that owns this skill.</param>
    /// <param name="name">Slug-style name (max 64 chars).</param>
    /// <param name="description">Description advertised to the agent (max 1024 chars).</param>
    /// <param name="instructions">Full instructions loaded on demand (max 5000 chars).</param>
    /// <returns>A new <see cref="Skill"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static Skill Create(Guid workspaceId, string name, string description, string instructions)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID cannot be empty.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill name cannot be empty.", nameof(name));

        if (name.Length > 64)
            throw new ArgumentException("Skill name cannot exceed 64 characters.", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Skill description cannot be empty.", nameof(description));

        if (description.Length > 1024)
            throw new ArgumentException("Skill description cannot exceed 1024 characters.", nameof(description));

        if (string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("Skill instructions cannot be empty.", nameof(instructions));

        return new Skill
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            Description = description.Trim(),
            Instructions = instructions.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates the skill's editable fields.
    /// </summary>
    /// <param name="name">New slug-style name (max 64 chars).</param>
    /// <param name="description">New description (max 1024 chars).</param>
    /// <param name="instructions">New full instructions (max 5000 chars).</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Update(string name, string description, string instructions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Skill name cannot be empty.", nameof(name));

        if (name.Length > 64)
            throw new ArgumentException("Skill name cannot exceed 64 characters.", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Skill description cannot be empty.", nameof(description));

        if (description.Length > 1024)
            throw new ArgumentException("Skill description cannot exceed 1024 characters.", nameof(description));

        if (string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("Skill instructions cannot be empty.", nameof(instructions));

        Name = name.Trim();
        Description = description.Trim();
        Instructions = instructions.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
