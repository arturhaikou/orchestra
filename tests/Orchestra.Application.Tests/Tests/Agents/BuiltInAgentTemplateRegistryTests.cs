using Orchestra.Application.Agents.Templates;

namespace Orchestra.Application.Tests.Tests.Agents;

public class BuiltInAgentTemplateRegistryTests
{
    private readonly IBuiltInAgentTemplateRegistry _registry;

    public BuiltInAgentTemplateRegistryTests()
    {
        _registry = new BuiltInAgentTemplateRegistry();
    }

    [Fact]
    public void GetAll_ReturnsCollectionContainingCodeReviewTemplate()
    {
        var templates = _registry.GetAll();

        Assert.NotEmpty(templates);

        var codeReview = templates.Single(t => t.Identifier == "code-review");
        Assert.Equal(1, codeReview.Version);
        Assert.Equal("Code Review Agent", codeReview.DisplayName);
        Assert.Equal("Automated code reviewer", codeReview.Role);
        Assert.Contains("Code Review", codeReview.Capabilities);
        Assert.Equal(IntegrationType.CODE_SOURCE, codeReview.RequiredIntegrationType);
        Assert.Contains("review_pull_request", codeReview.ToolActionMethodNames);
        Assert.Contains("review_merge_request", codeReview.ToolActionMethodNames);
    }

    [Fact]
    public void GetAll_CodeReviewTemplate_HasCorrectLockedFields()
    {
        var templates = _registry.GetAll();
        var codeReview = templates.Single(t => t.Identifier == "code-review");

        Assert.Contains("name", codeReview.LockedFields);
        Assert.Contains("role", codeReview.LockedFields);
        Assert.Contains("capabilities", codeReview.LockedFields);
        Assert.Contains("tools", codeReview.LockedFields);
    }

    [Fact]
    public void GetAll_CodeReviewTemplate_HasCorrectEditableFields()
    {
        var templates = _registry.GetAll();
        var codeReview = templates.Single(t => t.Identifier == "code-review");

        Assert.Contains("projectPrinciples", codeReview.EditableFields);
    }

    [Fact]
    public void GetAll_CodeReviewTemplate_HasProviderLabelMap()
    {
        var templates = _registry.GetAll();
        var codeReview = templates.Single(t => t.Identifier == "code-review");

        Assert.Equal("Pull Request", codeReview.ProviderLabelMap[ProviderType.GITHUB]);
        Assert.Equal("Merge Request", codeReview.ProviderLabelMap[ProviderType.GITLAB]);
    }

    [Fact]
    public void GetAll_CodeReviewTemplate_HasGuideTemplate()
    {
        var templates = _registry.GetAll();
        var codeReview = templates.Single(t => t.Identifier == "code-review");

        Assert.Contains("{providerLabel}", codeReview.GuideTemplate);
    }

    [Fact]
    public void GetByIdentifier_WithCodeReview_ReturnsCodeReviewTemplate()
    {
        var template = _registry.GetByIdentifier("code-review");

        Assert.NotNull(template);
        Assert.Equal("code-review", template.Identifier);
        Assert.Equal("Code Review Agent", template.DisplayName);
        Assert.Equal("Automated code reviewer", template.Role);
        Assert.Contains("Code Review", template.Capabilities);
        Assert.Contains("review_pull_request", template.ToolActionMethodNames);
        Assert.Contains("review_merge_request", template.ToolActionMethodNames);
    }

    [Fact]
    public void GetByIdentifier_WithUnknownIdentifier_ReturnsNull()
    {
        var template = _registry.GetByIdentifier("unknown-template");

        Assert.Null(template);
    }

    [Fact]
    public void GetByIdentifier_WithEmptyString_ReturnsNull()
    {
        var template = _registry.GetByIdentifier("");

        Assert.Null(template);
    }

    [Fact]
    public void GetAll_ReturnsImmutableCollection()
    {
        var first = _registry.GetAll();
        var second = _registry.GetAll();

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first[0].Identifier, second[0].Identifier);
    }

    [Fact]
    public void GetByIdentifier_IsCaseSensitive()
    {
        var template = _registry.GetByIdentifier("Code-Review");

        Assert.Null(template);
    }
}
