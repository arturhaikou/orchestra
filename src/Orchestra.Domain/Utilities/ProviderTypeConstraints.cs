using Orchestra.Domain.Enums;

namespace Orchestra.Domain.Utilities;

/// <summary>
/// Defines the authoritative per-provider allowed-type rules.
/// Jira supports only Tracker; Confluence supports only KnowledgeBase;
/// GitHub and GitLab support any non-empty subset of {Tracker, KnowledgeBase, CodeSource}.
/// All other providers (Azure DevOps, Linear, etc.) are unconstrained — they accept any type.
/// </summary>
public static class ProviderTypeConstraints
{
    private static readonly IReadOnlyDictionary<ProviderType, IReadOnlySet<IntegrationType>> AllowedTypeMap =
        new Dictionary<ProviderType, IReadOnlySet<IntegrationType>>
        {
            [ProviderType.JIRA]       = new HashSet<IntegrationType> { IntegrationType.TRACKER },
            [ProviderType.CONFLUENCE] = new HashSet<IntegrationType> { IntegrationType.KNOWLEDGE_BASE },
            [ProviderType.GITHUB]     = new HashSet<IntegrationType> { IntegrationType.TRACKER, IntegrationType.KNOWLEDGE_BASE, IntegrationType.CODE_SOURCE },
            [ProviderType.GITLAB]     = new HashSet<IntegrationType> { IntegrationType.TRACKER, IntegrationType.KNOWLEDGE_BASE, IntegrationType.CODE_SOURCE },
        };

    /// <summary>
    /// Returns the set of integration types that are permitted for <paramref name="provider"/>.
    /// For providers not in the constraint map (e.g., Azure DevOps, Linear), all three types are allowed.
    /// </summary>
    public static IReadOnlySet<IntegrationType> GetAllowedTypes(ProviderType provider)
    {
        if (AllowedTypeMap.TryGetValue(provider, out var allowed))
            return allowed;

        return new HashSet<IntegrationType>(Enum.GetValues<IntegrationType>());
    }
}
