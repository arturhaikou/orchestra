using Orchestra.Application.Agents.Services;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tests.Tests.Agents;

public class TemplateRegistryTests
{
    private readonly TemplateRegistry _sut = new();

    [Fact]
    public void GetAllTemplates_ReturnsNonEmptyList()
    {
        var templates = _sut.GetAllTemplates();

        Assert.NotEmpty(templates);
    }

    [Fact]
    public void GetAllTemplates_ContainsCodeReviewTemplate()
    {
        var templates = _sut.GetAllTemplates();

        Assert.Contains(templates, t => t.TemplateId == "code-review");
    }

    [Fact]
    public void GetTemplate_WithValidId_ReturnsTemplate()
    {
        var template = _sut.GetTemplate("code-review");

        Assert.NotNull(template);
        Assert.Equal("code-review", template.TemplateId);
    }

    [Fact]
    public void GetTemplate_WithUnknownId_ReturnsNull()
    {
        var template = _sut.GetTemplate("nonexistent-template");

        Assert.Null(template);
    }

    [Fact]
    public void GetTemplate_CodeReview_HasCorrectName()
    {
        var template = _sut.GetTemplate("code-review");

        Assert.NotNull(template);
        Assert.Equal("Code Review Agent", template.Name);
    }

    [Fact]
    public void GetTemplate_CodeReview_RequiresCodeSourceIntegration()
    {
        var template = _sut.GetTemplate("code-review");

        Assert.NotNull(template);
        Assert.Contains(IntegrationType.CODE_SOURCE, template.RequiredIntegrationTypes);
    }

    [Fact]
    public void GetTemplate_CodeReview_HasToolMethodNames()
    {
        var template = _sut.GetTemplate("code-review");

        Assert.NotNull(template);
        Assert.NotEmpty(template.ToolMethodNames);
    }

    [Fact]
    public void GetTemplate_CodeReview_HasLockedFields()
    {
        var template = _sut.GetTemplate("code-review");

        Assert.NotNull(template);
        Assert.NotEmpty(template.LockedFields);
    }

    [Fact]
    public void GetTemplate_CodeReview_HasPositiveVersion()
    {
        var template = _sut.GetTemplate("code-review");

        Assert.NotNull(template);
        Assert.True(template.TemplateVersion > 0);
    }

    [Fact]
    public void GetAllTemplates_AllTemplatesHaveUniqueIds()
    {
        var templates = _sut.GetAllTemplates();
        var ids = templates.Select(t => t.TemplateId).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
