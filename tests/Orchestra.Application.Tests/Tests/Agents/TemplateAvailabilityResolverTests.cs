using NSubstitute;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;
using Microsoft.Extensions.Logging;

namespace Orchestra.Application.Tests.Tests.Agents;

public class TemplateAvailabilityResolverTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ReviewPrToolActionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReviewMrToolActionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly IIntegrationService _integrationService = Substitute.For<IIntegrationService>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAiCliIntegrationDataAccess _aiCliIntegrationDataAccess = Substitute.For<IAiCliIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly ILogger<TemplateAvailabilityResolver> _logger = Substitute.For<ILogger<TemplateAvailabilityResolver>>();
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry = new BuiltInAgentTemplateRegistry();

    private TemplateAvailabilityResolver BuildSut()
    {
        return new TemplateAvailabilityResolver(
            _integrationService,
            _integrationDataAccess,
            _aiCliIntegrationDataAccess,
            _agentDataAccess,
            _toolActionDataAccess,
            _logger,
            _templateRegistry);
    }

    private void SetupIntegrations(params IntegrationDto[] integrations)
    {
        _integrationService
            .GetWorkspaceIntegrationsAsync(UserId, WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(integrations.ToList());
    }

    private void SetupNoDeployedTemplateAgents()
    {
        _agentDataAccess
            .GetTemplateAgentsByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent>());
    }

    private void SetupToolActions(params (string methodName, Guid id)[] actions)
    {
        _toolActionDataAccess
            .GetByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var requestedNames = callInfo.ArgAt<List<string>>(0);
                return actions
                    .Where(a => requestedNames.Contains(a.methodName))
                    .Select(a => CreateToolActionWithId(a.id, a.methodName))
                    .ToList();
            });
    }

    private static ToolAction CreateToolActionWithId(Guid id, string methodName)
    {
        var toolAction = ToolAction.Create(Guid.NewGuid(), methodName, null, methodName, DangerLevel.Safe);
        typeof(ToolAction).GetProperty(nameof(ToolAction.Id))!
            .SetValue(toolAction, id);
        return toolAction;
    }

    private static IntegrationDto CreateGitHubIntegration()
    {
        return new IntegrationDto(
            Id: Guid.NewGuid().ToString(),
            WorkspaceId: WorkspaceId.ToString(),
            Name: "GitHub",
            Types: new[] { "CODE_SOURCE" },
            Icon: null,
            Provider: "GITHUB",
            Url: "https://github.com",
            Username: null,
            Connected: true,
            LastSync: null,
            FilterQuery: null,
            Vectorize: false);
    }

    private static IntegrationDto CreateGitLabIntegration()
    {
        return new IntegrationDto(
            Id: Guid.NewGuid().ToString(),
            WorkspaceId: WorkspaceId.ToString(),
            Name: "GitLab",
            Types: new[] { "CODE_SOURCE" },
            Icon: null,
            Provider: "GITLAB",
            Url: "https://gitlab.com",
            Username: null,
            Connected: true,
            LastSync: null,
            FilterQuery: null,
            Vectorize: false);
    }

    private static IntegrationDto CreateJiraIntegration()
    {
        return new IntegrationDto(
            Id: Guid.NewGuid().ToString(),
            WorkspaceId: WorkspaceId.ToString(),
            Name: "Jira",
            Types: new[] { "TRACKER" },
            Icon: null,
            Provider: "JIRA",
            Url: "https://jira.example.com",
            Username: null,
            Connected: true,
            LastSync: null,
            FilterQuery: null,
            Vectorize: false);
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WithGitHubIntegration_ReturnsAvailableWithPullRequestLabel()
    {
        SetupIntegrations(CreateGitHubIntegration());
        SetupNoDeployedTemplateAgents();
        SetupToolActions(("review_pull_request", ReviewPrToolActionId));

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.Equal(TemplateAvailabilityStatus.Available, codeReview.Status);
        Assert.Contains(codeReview.ResolvedToolActions, t => t.MethodName == "review_pull_request" && t.ToolActionId == ReviewPrToolActionId);
        Assert.Contains(codeReview.ProviderLabels, l => l.Label == "Pull Request");
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WithGitLabIntegration_ReturnsAvailableWithMergeRequestLabel()
    {
        SetupIntegrations(CreateGitLabIntegration());
        SetupNoDeployedTemplateAgents();
        SetupToolActions(("review_merge_request", ReviewMrToolActionId));

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.Equal(TemplateAvailabilityStatus.Available, codeReview.Status);
        Assert.Contains(codeReview.ResolvedToolActions, t => t.MethodName == "review_merge_request" && t.ToolActionId == ReviewMrToolActionId);
        Assert.Contains(codeReview.ProviderLabels, l => l.Label == "Merge Request");
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WithBothProviders_ReturnsAvailableWithBothLabelsAndToolActions()
    {
        SetupIntegrations(CreateGitHubIntegration(), CreateGitLabIntegration());
        SetupNoDeployedTemplateAgents();
        SetupToolActions(
            ("review_pull_request", ReviewPrToolActionId),
            ("review_merge_request", ReviewMrToolActionId));

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.Equal(TemplateAvailabilityStatus.Available, codeReview.Status);
        Assert.Equal(2, codeReview.ResolvedToolActions.Count);
        Assert.Equal(2, codeReview.ProviderLabels.Count);
        Assert.Contains(codeReview.ProviderLabels, l => l.Label == "Pull Request");
        Assert.Contains(codeReview.ProviderLabels, l => l.Label == "Merge Request");
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WithNoCodeSourceIntegration_ReturnsUnavailableWithReason()
    {
        SetupIntegrations(CreateJiraIntegration());
        SetupNoDeployedTemplateAgents();

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.Equal(TemplateAvailabilityStatus.Unavailable, codeReview.Status);
        Assert.NotNull(codeReview.UnavailabilityReason);
        Assert.Contains("Code Source", codeReview.UnavailabilityReason);
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WhenTemplateAlreadyDeployed_ReturnsAlreadyDeployedWithAgentId()
    {
        SetupIntegrations(CreateGitHubIntegration());

        var existingAgent = new AgentBuilder()
            .WithWorkspaceId(WorkspaceId)
            .WithName("Code Review Agent")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        _agentDataAccess
            .GetTemplateAgentsByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent> { existingAgent });

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.Equal(TemplateAvailabilityStatus.AlreadyDeployed, codeReview.Status);
        Assert.NotNull(codeReview.ExistingAgentId);
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WhenToolActionNotSeeded_ReturnsErrorStatus()
    {
        SetupIntegrations(CreateGitHubIntegration());
        SetupNoDeployedTemplateAgents();

        _toolActionDataAccess
            .GetByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.Equal(TemplateAvailabilityStatus.Error, codeReview.Status);
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WithNoIntegrations_ReturnsUnavailable()
    {
        SetupIntegrations();
        SetupNoDeployedTemplateAgents();

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.Equal(TemplateAvailabilityStatus.Unavailable, codeReview.Status);
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WithGitHubOnly_SubstitutesGuideWithPullRequest()
    {
        SetupIntegrations(CreateGitHubIntegration());
        SetupNoDeployedTemplateAgents();
        SetupToolActions(("review_pull_request", ReviewPrToolActionId));

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.NotNull(codeReview.ResolvedGuide);
        Assert.Contains("Pull Request", codeReview.ResolvedGuide);
        Assert.DoesNotContain("{{PROVIDER_LABEL}}", codeReview.ResolvedGuide);
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WithBothProviders_SubstitutesGuideWithBothLabels()
    {
        SetupIntegrations(CreateGitHubIntegration(), CreateGitLabIntegration());
        SetupNoDeployedTemplateAgents();
        SetupToolActions(
            ("review_pull_request", ReviewPrToolActionId),
            ("review_merge_request", ReviewMrToolActionId));

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        var codeReview = results.First(r => r.TemplateId == "code-review");
        Assert.NotNull(codeReview.ResolvedGuide);
        Assert.Contains("Pull Request", codeReview.ResolvedGuide);
        Assert.Contains("Merge Request", codeReview.ResolvedGuide);
    }

    [Fact]
    public async Task ResolveAvailabilityAsync_WhenOneTemplateErrors_OtherTemplatesStillResolved()
    {
        SetupIntegrations(CreateGitHubIntegration());
        SetupNoDeployedTemplateAgents();

        _toolActionDataAccess
            .GetByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var sut = BuildSut();

        var results = await sut.ResolveAvailabilityAsync(UserId, WorkspaceId);

        Assert.NotEmpty(results);
    }
}
