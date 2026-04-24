using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.Services;

public class TemplateRegistry : ITemplateRegistry
{
    private static readonly IReadOnlyList<TemplateDefinition> Templates = new List<TemplateDefinition>
    {
        new(
            TemplateId: "code-review",
            Name: "Code Review Agent",
            Role: "Automatically reviews pull requests, identifies issues, and suggests improvements using configurable project principles.",
            Description: "Automatically reviews pull requests, identifies issues, and suggests improvements using configurable project principles.",
            RequiredIntegrationTypes: new[] { IntegrationType.CODE_SOURCE },
            ToolMethodNames: new[] { "review_pull_request", "review_merge_request" },
            LockedFields: new[] { "Name", "Role", "Capabilities" },
            DefaultCapabilities: new[] { "code_review", "pull_request_analysis" },
            TemplateVersion: 1
        )
    };

    private static readonly Dictionary<string, TemplateDefinition> TemplateMap =
        Templates.ToDictionary(t => t.TemplateId, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<(string Identifier, int Version), string> GuideTexts = new()
    {
        [("code-review", 1)] = "This agent automatically reviews {{CODE_CHANGE_NOUN}}s when a ticket is assigned. " +
                               "It analyzes code changes, identifies issues, and posts review comments directly " +
                               "on the {{CODE_CHANGE_NOUN}}."
    };

    public IReadOnlyList<TemplateDefinition> GetAllTemplates() => Templates;

    public TemplateDefinition? GetTemplate(string templateId)
    {
        return TemplateMap.GetValueOrDefault(templateId);
    }

    public string? GetGuideText(string templateIdentifier, int templateVersion)
    {
        return GuideTexts.GetValueOrDefault((templateIdentifier, templateVersion));
    }
}
