using NSubstitute;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;
using Microsoft.Extensions.Logging;

namespace Orchestra.Application.Tests.Tests.Agents;

public class ValidatePrerequisitesTests
{
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly IIntegrationService _integrationService = Substitute.For<IIntegrationService>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly ILogger<TemplateAvailabilityResolver> _logger = Substitute.For<ILogger<TemplateAvailabilityResolver>>();
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry = new BuiltInAgentTemplateRegistry();

    private TemplateAvailabilityResolver BuildSut()
    {
        return new TemplateAvailabilityResolver(
            _integrationService,
            _integrationDataAccess,
            _agentDataAccess,
            _toolActionDataAccess,
            _logger,
            _templateRegistry);
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
            GuideTemplate: "Review guide",
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

    private static Integration CreateGitHubIntegration()
    {
        return new Integration(
            id: Guid.NewGuid(),
            workspaceId: WorkspaceId,
            name: "GitHub",
            provider: ProviderType.GITHUB,
            url: "https://github.com",
            apiKey: "token",
            filterQuery: null,
            integrationTypes: new[] { IntegrationType.CODE_SOURCE });
    }

    private static Integration CreateJiraIntegration()
    {
        return new Integration(
            id: Guid.NewGuid(),
            workspaceId: WorkspaceId,
            name: "Jira",
            provider: ProviderType.JIRA,
            url: "https://jira.example.com",
            apiKey: "token",
            filterQuery: null,
            integrationTypes: new[] { IntegrationType.TRACKER });
    }

    [Fact]
    public async Task ValidatePrerequisitesAsync_WithActiveCodeSourceIntegration_DoesNotThrow()
    {
        var template = CreateCodeReviewTemplate();
        var integration = CreateGitHubIntegration();

        _integrationDataAccess
            .GetByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _agentDataAccess
            .GetTemplateAgentsByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent>());

        var sut = BuildSut();

        await sut.ValidatePrerequisitesAsync(WorkspaceId, template);
    }

    [Fact]
    public async Task ValidatePrerequisitesAsync_WithNoCodeSourceIntegration_ThrowsIntegrationRequiredException()
    {
        var template = CreateCodeReviewTemplate();

        _integrationDataAccess
            .GetByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration>());
        _agentDataAccess
            .GetTemplateAgentsByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent>());

        var sut = BuildSut();

        await Assert.ThrowsAsync<IntegrationRequiredException>(
            () => sut.ValidatePrerequisitesAsync(WorkspaceId, template));
    }

    [Fact]
    public async Task ValidatePrerequisitesAsync_WithOnlyTrackerIntegration_ThrowsIntegrationRequiredException()
    {
        var template = CreateCodeReviewTemplate();
        var integration = CreateJiraIntegration();

        _integrationDataAccess
            .GetByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _agentDataAccess
            .GetTemplateAgentsByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent>());

        var sut = BuildSut();

        await Assert.ThrowsAsync<IntegrationRequiredException>(
            () => sut.ValidatePrerequisitesAsync(WorkspaceId, template));
    }

    [Fact]
    public async Task ValidatePrerequisitesAsync_WithTemplateAlreadyDeployed_ThrowsTemplateAlreadyDeployedException()
    {
        var template = CreateCodeReviewTemplate();
        var existingAgent = new AgentBuilder()
            .WithWorkspaceId(WorkspaceId)
            .WithName("Code Review Agent")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();
        var integration = CreateGitHubIntegration();

        _integrationDataAccess
            .GetByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { integration });
        _agentDataAccess
            .GetTemplateAgentsByWorkspaceIdAsync(WorkspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Agent> { existingAgent });

        var sut = BuildSut();

        await Assert.ThrowsAsync<TemplateAlreadyDeployedException>(
            () => sut.ValidatePrerequisitesAsync(WorkspaceId, template));
    }
}
