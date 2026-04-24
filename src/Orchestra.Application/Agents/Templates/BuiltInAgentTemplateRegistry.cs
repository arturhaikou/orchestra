using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.Templates;

public class BuiltInAgentTemplateRegistry : IBuiltInAgentTemplateRegistry
{
    private readonly Dictionary<string, BuiltInAgentTemplate> _templates = new();

    public BuiltInAgentTemplateRegistry()
    {
        Register(CreateCodeReviewTemplate());
    }

    public IReadOnlyList<BuiltInAgentTemplate> GetAll()
    {
        return _templates.Values.ToList().AsReadOnly();
    }

    public BuiltInAgentTemplate? GetByIdentifier(string identifier)
    {
        _templates.TryGetValue(identifier, out var template);
        return template;
    }

    private void Register(BuiltInAgentTemplate template)
    {
        if (!_templates.TryAdd(template.Identifier, template))
            throw new InvalidOperationException($"Duplicate template identifier: {template.Identifier}");
    }

    private static BuiltInAgentTemplate CreateCodeReviewTemplate()
    {
        return new BuiltInAgentTemplate(
            Identifier: "code-review",
            Version: 1,
            DisplayName: "Code Review Agent",
            Role: "Automated code reviewer",
            Capabilities: new[] { "Code Review" },
            RequiredIntegrationType: IntegrationType.CODE_SOURCE,
            ToolActionMethodNames: new[] { "review_pull_request", "review_merge_request" },
            LockedFields: new HashSet<string> { "name", "role", "capabilities", "tools" },
            EditableFields: new[] { "projectPrinciples" },
            GuideTemplate: "Create a ticket and provide a {providerLabel} link. The agent will automatically review the code changes based on your project principles.",
            ProviderLabelMap: new Dictionary<ProviderType, string>
            {
                { ProviderType.GITHUB, "Pull Request" },
                { ProviderType.GITLAB, "Merge Request" }
            },
            ProviderToolMethodMap: new Dictionary<ProviderType, string>
            {
                { ProviderType.GITHUB, "review_pull_request" },
                { ProviderType.GITLAB, "review_merge_request" }
            });
    }
}
