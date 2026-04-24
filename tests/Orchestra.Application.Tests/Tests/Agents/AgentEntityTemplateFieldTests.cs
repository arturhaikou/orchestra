using Orchestra.Domain.Entities;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentEntityTemplateFieldTests
{
    [Fact]
    public void Create_WithBothTemplateFieldsNull_CreatesCustomAgent()
    {
        var agent = new AgentBuilder()
            .WithTemplateIdentifier(null)
            .WithTemplateVersion(null)
            .Build();

        Assert.Null(agent.TemplateIdentifier);
        Assert.Null(agent.TemplateVersion);
    }

    [Fact]
    public void Create_WithBothTemplateFieldsPopulated_CreatesTemplateAgent()
    {
        var agent = new AgentBuilder()
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        Assert.Equal("code-review", agent.TemplateIdentifier);
        Assert.Equal(1, agent.TemplateVersion);
    }

    [Fact]
    public void Create_WithTemplateIdentifierOnly_ThrowsArgumentException()
    {
        var builder = new AgentBuilder()
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(null);

        var exception = Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Contains("TemplateIdentifier and TemplateVersion must both be null or both be non-null", exception.Message);
    }

    [Fact]
    public void Create_WithTemplateVersionOnly_ThrowsArgumentException()
    {
        var builder = new AgentBuilder()
            .WithTemplateIdentifier(null)
            .WithTemplateVersion(1);

        var exception = Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Contains("TemplateIdentifier and TemplateVersion must both be null or both be non-null", exception.Message);
    }

    [Fact]
    public void Create_WithTemplateIdentifierExceeding200Chars_ThrowsArgumentException()
    {
        var longIdentifier = new string('a', 201);

        var builder = new AgentBuilder()
            .WithTemplateIdentifier(longIdentifier)
            .WithTemplateVersion(1);

        var exception = Assert.Throws<ArgumentException>(() => builder.Build());
        Assert.Contains("TemplateIdentifier cannot exceed 200 characters", exception.Message);
    }

    [Fact]
    public void Create_WithDefaultBuilder_HasNullTemplateFields()
    {
        var agent = new AgentBuilder().Build();

        Assert.Null(agent.TemplateIdentifier);
        Assert.Null(agent.TemplateVersion);
    }
}