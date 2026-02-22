using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Utilities;

/// <summary>
/// Detects Jira and Confluence instance types (Cloud vs On-Premise) based on the integration URL.
/// Cloud instances are hosted on *.atlassian.net; any other domain is treated as On-Premise.
/// </summary>
public static class IntegrationTypeDetector
{
    private const string AtlassianCloudSuffix = ".atlassian.net";

    /// <summary>
    /// Detects the Jira instance type from the integration URL.
    /// Returns <see cref="JiraType.Cloud"/> when the host ends with <c>.atlassian.net</c>,
    /// otherwise returns <see cref="JiraType.OnPremise"/>.
    /// </summary>
    public static JiraType DetectJiraType(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return JiraType.Cloud;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Host.EndsWith(AtlassianCloudSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return JiraType.Cloud;
        }

        return JiraType.OnPremise;
    }

    /// <summary>
    /// Detects the Confluence instance type from the integration URL.
    /// Returns <see cref="ConfluenceType.Cloud"/> when the host ends with <c>.atlassian.net</c>,
    /// otherwise returns <see cref="ConfluenceType.OnPremise"/>.
    /// </summary>
    public static ConfluenceType DetectConfluenceType(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return ConfluenceType.Cloud;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Host.EndsWith(AtlassianCloudSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return ConfluenceType.Cloud;
        }

        return ConfluenceType.OnPremise;
    }
}
