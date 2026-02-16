namespace Orchestra.Domain.Enums;

/// <summary>
/// Defines the specific external service providers supported by the system.
/// </summary>
public enum ProviderType
{
    /// <summary>
    /// Atlassian Jira issue tracking system.
    /// </summary>
    JIRA = 0,

    /// <summary>
    /// Microsoft Azure DevOps issue tracking and project management.
    /// </summary>
    AZURE_DEVOPS = 1,

    /// <summary>
    /// Linear issue tracking and project management.
    /// </summary>
    LINEAR = 2,

    /// <summary>
    /// GitHub code repository and issue tracking.
    /// </summary>
    GITHUB = 3,

    /// <summary>
    /// GitLab code repository and issue tracking.
    /// </summary>
    GITLAB = 4,

    /// <summary>
    /// Atlassian Confluence knowledge base and documentation.
    /// </summary>
    CONFLUENCE = 5,

    /// <summary>
    /// Notion all-in-one workspace and documentation.
    /// </summary>
    NOTION = 6,

    /// <summary>
    /// Custom or generic provider implementation.
    /// </summary>
    CUSTOM = 7,

    /// <summary>
    /// Internal tools that don't require external integration.
    /// Used for tool categories that are always available.
    /// </summary>
    INTERNAL = 8
}