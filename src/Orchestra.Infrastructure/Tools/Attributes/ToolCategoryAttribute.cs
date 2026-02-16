using System;
using Orchestra.Domain.Enums;

namespace Orchestra.Infrastructure.Tools.Attributes;

/// <summary>
/// Decorates tool service interfaces or classes to define tool category metadata
/// for automatic discovery and registration by the ToolScanningService.
/// </summary>
/// <remarks>
/// This attribute enables declarative tool definitions directly on service interfaces,
/// making tool discovery automatic and reducing manual registration overhead.
/// The scanning service reads these attributes via reflection to populate the database.
/// </remarks>
/// <example>
/// <code>
/// [ToolCategory("Jira", ProviderType.JIRA, "Manage Jira issues and projects")]
/// public interface IJiraToolService
/// {
///     [ToolAction("create_issue", "Create a new Jira issue")]
///     Task&lt;object&gt; CreateIssueAsync();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ToolCategoryAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the tool category (e.g., "Jira", "GitHub").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the provider type that this tool category belongs to.
    /// Used to associate tools with specific integrations.
    /// </summary>
    public ProviderType ProviderType { get; }

    /// <summary>
    /// Gets the description of what this tool category does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the ToolCategoryAttribute class.
    /// </summary>
    /// <param name="name">The name of the tool category. Cannot be null or empty.</param>
    /// <param name="providerType">The provider type for this tool category.</param>
    /// <param name="description">A description of the tool category's purpose. Cannot be null or empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when name or description is null.</exception>
    public ToolCategoryAttribute(string name, ProviderType providerType, string description)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        Name = name;
        ProviderType = providerType;
        Description = description;
    }
}