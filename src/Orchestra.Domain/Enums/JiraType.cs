namespace Orchestra.Domain.Enums;

/// <summary>
/// Defines the Jira instance types supported by the system.
/// </summary>
public enum JiraType
{
    /// <summary>
    /// Jira Cloud instance using REST API v3.
    /// Accessed via https://[domain].atlassian.net
    /// </summary>
    Cloud = 0,

    /// <summary>
    /// Jira Data Center or Server instance using REST API v2.
    /// Accessed via self-hosted or on-premise URL.
    /// </summary>
    OnPremise = 1
}
