using System;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Tools.Attributes;

/// <summary>
/// Decorates tool service methods to define tool action metadata
/// for automatic discovery and registration by the ToolScanningService.
/// </summary>
/// <remarks>
/// This attribute enables declarative tool action definitions directly on service methods.
/// The scanning service reads these attributes via reflection to populate the ToolAction
/// table in the database with method names and descriptions.
/// </remarks>
/// <example>
/// <code>
/// [ToolCategory("Jira", ProviderType.JIRA, "Manage Jira issues and projects")]
/// public interface IJiraToolService
/// {
///     [ToolAction("create_issue", "Create a new Jira issue with specified fields", DangerLevel.Moderate)]
///     Task&lt;object&gt; CreateIssueAsync(string projectKey, string summary, string description);
///     
///     [ToolAction("get_issue", "Retrieve details of a specific Jira issue by key", DangerLevel.Safe)]
///     Task&lt;object&gt; GetIssueAsync(string issueKey);
///     
///     [ToolAction("delete_issue", "Delete a Jira issue permanently", DangerLevel.Destructive)]
///     Task&lt;object&gt; DeleteIssueAsync(string issueKey);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ToolActionAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the tool action (e.g., "create_issue", "get_issue").
    /// This name is used by agents to reference and invoke the tool.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of what this tool action does.
    /// Used for display in UI and to help agents understand the tool's purpose.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the danger level of this tool action (Safe, Moderate, or Destructive).
    /// Safe: Read operations, no data modification.
    /// Moderate: Create/update operations, reversible changes.
    /// Destructive: Delete operations, permanent changes.
    /// </summary>
    public DangerLevel DangerLevel { get; }

    /// <summary>
    /// Initializes a new instance of the ToolActionAttribute class.
    /// </summary>
    /// <param name="name">The name of the tool action. Cannot be null or empty. Should use snake_case convention.</param>
    /// <param name="description">A description of what the tool action does. Cannot be null or empty.</param>
    /// <param name="dangerLevel">The danger level of this action (defaults to Safe).</param>
    /// <exception cref="ArgumentNullException">Thrown when name or description is null.</exception>
    public ToolActionAttribute(string name, string description, DangerLevel dangerLevel = DangerLevel.Safe)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        Name = name;
        Description = description;
        DangerLevel = dangerLevel;
    }
}