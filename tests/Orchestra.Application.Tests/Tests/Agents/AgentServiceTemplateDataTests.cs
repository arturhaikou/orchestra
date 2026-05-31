using NSubstitute;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceTemplateDataTests
{
    private static (
        AgentService service,
        IAgentDataAccess agentDataAccess,
        IBuiltInAgentTemplateRegistry templateRegistry,
        IIntegrationDataAccess integrationDataAccess)
        BuildSut()
    {
        var agentDataAccess = Substitute.For<IAgentDataAccess>();
        var authService = Substitute.For<IWorkspaceAuthorizationService>();
        var toolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
        var toolValidationService = Substitute.For<IToolValidationService>();
        var integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
        var templateRegistry = Substitute.For<IBuiltInAgentTemplateRegistry>();

        toolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        toolActionDataAccess
            .GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        var service = new AgentService(
            agentDataAccess,
            toolActionDataAccess,
            Substitute.For<IAgentMcpToolDataAccess>(),
            Substitute.For<IAgentSubAgentDataAccess>(),
            Substitute.For<IAgentSkillDataAccess>(),
            Substitute.For<ISkillDataAccess>(),
            authService,
            toolValidationService,
            templateRegistry,
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            integrationDataAccess,
            Substitute.For<IAgentSubAgentAssignmentService>());

        return (service, agentDataAccess, templateRegistry, integrationDataAccess);
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

    [Fact]
    public async Task GetAgentByIdAsync_BuiltInAgentWithGitHub_ReturnsDtoWithTemplateDataAndPullRequestGuide()
    {
        var (sut, agentDataAccess, templateRegistry, integrationDataAccess) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);

        templateRegistry.GetByIdentifier("code-review")
            .Returns(CreateCodeReviewTemplate());

        var githubIntegration = Integration.Create(
            workspaceId, "GitHub", new[] { IntegrationType.CODE_SOURCE },
            ProviderType.GITHUB);
        integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { githubIntegration });

        var result = await sut.GetAgentByIdAsync(userId, agent.Id);

        Assert.Equal("code-review", result.TemplateId);
        Assert.Equal(1, result.TemplateVersion);
        Assert.NotNull(result.Guide);
        Assert.Contains("Pull Request", result.Guide);
    }

    [Fact]
    public async Task GetAgentByIdAsync_CustomAgent_ReturnsDtoWithNullTemplateFields()
    {
        var (sut, agentDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);

        var result = await sut.GetAgentByIdAsync(userId, agent.Id);

        Assert.Null(result.TemplateId);
        Assert.Null(result.TemplateVersion);
        Assert.Null(result.Guide);
    }

    [Fact]
    public async Task GetAgentByIdAsync_BuiltInAgentWithGitLab_ReturnsDtoWithMergeRequestGuide()
    {
        var (sut, agentDataAccess, templateRegistry, integrationDataAccess) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);

        templateRegistry.GetByIdentifier("code-review")
            .Returns(CreateCodeReviewTemplate());

        var gitlabIntegration = Integration.Create(
            workspaceId, "GitLab", new[] { IntegrationType.CODE_SOURCE },
            ProviderType.GITLAB);
        integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { gitlabIntegration });

        var result = await sut.GetAgentByIdAsync(userId, agent.Id);

        Assert.Equal("code-review", result.TemplateId);
        Assert.Equal(1, result.TemplateVersion);
        Assert.NotNull(result.Guide);
        Assert.Contains("Merge Request", result.Guide);
        Assert.DoesNotContain("Pull Request", result.Guide);
    }

    [Fact]
    public async Task GetAgentByIdAsync_BuiltInAgentNoCodeSourceIntegration_DefaultsToPullRequest()
    {
        var (sut, agentDataAccess, templateRegistry, integrationDataAccess) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);

        templateRegistry.GetByIdentifier("code-review")
            .Returns(CreateCodeReviewTemplate());

        integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        var result = await sut.GetAgentByIdAsync(userId, agent.Id);

        Assert.NotNull(result.Guide);
        Assert.Contains("Pull Request", result.Guide);
    }

    [Fact]
    public async Task GetAgentByIdAsync_UnknownTemplateIdentifier_ReturnsNullGuide()
    {
        var (sut, agentDataAccess, templateRegistry, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithTemplateIdentifier("unknown-template")
            .WithTemplateVersion(99)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>())
            .Returns(agent);

        templateRegistry.GetByIdentifier("unknown-template")
            .Returns((BuiltInAgentTemplate?)null);

        var result = await sut.GetAgentByIdAsync(userId, agent.Id);

        Assert.Equal("unknown-template", result.TemplateId);
        Assert.Equal(99, result.TemplateVersion);
        Assert.Null(result.Guide);
    }

    [Fact]
    public async Task GetAgentsByWorkspaceIdAsync_MixedAgents_ReturnsCorrectTemplateDataForEach()
    {
        var (sut, agentDataAccess, templateRegistry, integrationDataAccess) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var builtInAgent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        var customAgent = new AgentBuilder()
            .WithWorkspaceId(workspaceId)
            .Build();

        agentDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent> { builtInAgent, customAgent });

        templateRegistry.GetByIdentifier("code-review")
            .Returns(CreateCodeReviewTemplate());

        var githubIntegration = Integration.Create(
            workspaceId, "GitHub", new[] { IntegrationType.CODE_SOURCE },
            ProviderType.GITHUB);
        integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { githubIntegration });

        var results = await sut.GetAgentsByWorkspaceIdAsync(userId, workspaceId);

        Assert.Equal(2, results.Count);

        var builtInDto = results.First(d => d.TemplateId != null);
        Assert.Equal("code-review", builtInDto.TemplateId);
        Assert.Equal(1, builtInDto.TemplateVersion);
        Assert.NotNull(builtInDto.Guide);
        Assert.Contains("Pull Request", builtInDto.Guide);

        var customDto = results.First(d => d.TemplateId == null);
        Assert.Null(customDto.TemplateVersion);
        Assert.Null(customDto.Guide);
    }

    [Fact]
    public async Task GetAgentsByWorkspaceIdAsync_MultipleAgents_QueriesIntegrationsOnce()
    {
        var (sut, agentDataAccess, templateRegistry, integrationDataAccess) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var agents = Enumerable.Range(0, 3)
            .Select(_ => new AgentBuilder()
                .WithWorkspaceId(workspaceId)
                .WithTemplateIdentifier("code-review")
                .WithTemplateVersion(1)
                .Build())
            .ToList();

        agentDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(agents);

        templateRegistry.GetByIdentifier("code-review")
            .Returns(CreateCodeReviewTemplate());

        integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());

        await sut.GetAgentsByWorkspaceIdAsync(userId, workspaceId);

        await integrationDataAccess.Received(1)
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>());
    }
}
