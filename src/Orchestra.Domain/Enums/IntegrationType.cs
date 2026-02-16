namespace Orchestra.Domain.Enums;

/// <summary>
/// Defines the category of external system integration.
/// </summary>
public enum IntegrationType
{
    /// <summary>
    /// Issue tracking systems (Jira, Azure DevOps, Linear).
    /// </summary>
    TRACKER = 0,
    
    /// <summary>
    /// Knowledge base systems (Confluence, Notion).
    /// </summary>
    KNOWLEDGE_BASE = 1,
    
    /// <summary>
    /// Code repository systems (GitHub, GitLab).
    /// </summary>
    CODE_SOURCE = 2
}
