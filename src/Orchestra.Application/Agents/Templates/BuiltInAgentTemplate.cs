using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.Templates;

public record BuiltInAgentTemplate(
    string Identifier,
    int Version,
    string DisplayName,
    string Role,
    IReadOnlyList<string> Capabilities,
    IntegrationType? RequiredIntegrationType,
    IReadOnlyList<string> ToolActionMethodNames,
    IReadOnlySet<string> LockedFields,
    IReadOnlyList<string> EditableFields,
    string GuideTemplate,
    IReadOnlyDictionary<ProviderType, string>? ProviderLabelMap,
    IReadOnlyDictionary<ProviderType, string>? ProviderToolMethodMap,
    bool IsCliAgent = false,
    string? DefaultCustomInstructions = null,
    bool IsReadOnlyCli = true);
