namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models;

/// <summary>
/// Represents a Jira Project.
/// </summary>
public class Project
{
    /// <summary>
    /// The unique identifier (ID) for the project.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The project key (e.g., PROJ).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// The project name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The project type.
    /// </summary>
    public string? Type { get; set; }
}
