using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Entities;

/// <summary>
/// Represents an individual executable tool method within a tool category.
/// Tool actions can be assigned to agents for granular capability management.
/// </summary>
public class ToolAction
{
    /// <summary>
    /// Gets the unique identifier of the tool action.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the identifier of the tool category this action belongs to.
    /// </summary>
    public Guid ToolCategoryId { get; private set; }

    /// <summary>
    /// Gets the display name of the tool action (e.g., "Create Issue").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description of what this tool action does.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Gets the method name to invoke via reflection (e.g., "CreateIssueAsync").
    /// </summary>
    public string MethodName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the danger level of this tool action (Safe, Moderate, or Destructive).
    /// </summary>
    public DangerLevel DangerLevel { get; private set; } = DangerLevel.Safe;

    /// <summary>
    /// Private constructor to enforce factory method usage.
    /// </summary>
    private ToolAction() { } // EF Core constructor

    /// <summary>
    /// Creates a new ToolAction instance with validation.
    /// </summary>
    /// <param name="toolCategoryId">The ID of the tool category this action belongs to.</param>
    /// <param name="name">The display name of the tool action.</param>
    /// <param name="description">The description of what this tool action does (optional).</param>
    /// <param name="methodName">The method name to invoke via reflection.</param>
    /// <param name="dangerLevel">The danger level of this action (defaults to Safe).</param>
    /// <returns>A new ToolAction instance.</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public static ToolAction Create(
        Guid toolCategoryId,
        string name,
        string? description,
        string methodName,
        DangerLevel dangerLevel = DangerLevel.Safe)
    {
        if (toolCategoryId == Guid.Empty)
            throw new ArgumentException("Tool category ID is required.", nameof(toolCategoryId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        if (string.IsNullOrWhiteSpace(methodName))
            throw new ArgumentException("Method name cannot be empty.", nameof(methodName));

        return new ToolAction
        {
            Id = Guid.NewGuid(),
            ToolCategoryId = toolCategoryId,
            Name = name,
            Description = description,
            MethodName = methodName,
            DangerLevel = dangerLevel
        };
    }

    /// <summary>
    /// Updates the tool action's metadata.
    /// </summary>
    /// <param name="name">The new display name (required).</param>
    /// <param name="description">The new description (optional).</param>
    /// <param name="dangerLevel">The new danger level.</param>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Update(string name, string? description, DangerLevel dangerLevel)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        Name = name;
        Description = description;
        DangerLevel = dangerLevel;
    }
}