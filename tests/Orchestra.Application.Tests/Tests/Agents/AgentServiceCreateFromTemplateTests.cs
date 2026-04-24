using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceCreateFromTemplateTests
{
    private readonly IAgentDataAccess _agentDataAccess;
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService;
    private readonly IToolValidationService _toolValidationService;
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry;
    private readonly ITemplateAvailabilityResolver _availabilityResolver;
    private readonly IToolActionDataAccess _toolActionDataAccess;
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly AgentService _sut;

    public AgentServiceCreateFromTemplateTests()
    {
        _agentDataAccess = Substitute.For<IAgentDataAccess>();
        _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
        _workspaceAuthorizationService = Substitute.For<IWorkspaceAuthorizationService>();
        _toolValidationService = Substitute.For<IToolValidationService>();
        _templateRegistry = Substitute.For<IBuiltInAgentTemplateRegistry>();
        _availabilityResolver = Substitute.For<ITemplateAvailabilityResolver>();
        _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
        _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();

        _sut = new AgentService(
            _agentDataAccess,
            _agentToolActionDataAccess,
            _workspaceAuthorizationService,
            _toolValidationService,
            _templateRegistry,
            _availabilityResolver,
            _toolActionDataAccess,
            _integrationDataAccess);
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

    private static CreateAgentFromTemplateRequest CreateValidRequest(Guid? workspaceId = null)
    {
        return new CreateAgentFromTemplateRequest(
            WorkspaceId: workspaceId ?? Guid.NewGuid(),
            TemplateId: "code-review",
            ProjectPrinciples: "## Standards\n- Follow SOLID principles",
            Model: null);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithGitHubIntegration_CreatesAgentWithReviewPullRequestAction()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var request = CreateValidRequest(workspaceId);
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);

        var gitHubIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.GITHUB)
            .WithType(IntegrationType.CODE_SOURCE)
            .Build();

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { gitHubIntegration });

        var toolAction = ToolAction.Create(Guid.NewGuid(), "Review Pull Request", "Reviews PRs", "review_pull_request", DangerLevel.Safe);
        _toolActionDataAccess.GetByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { toolAction });

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { toolAction.Id });
        _agentToolActionDataAccess.GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "Code Review" });

        var result = await _sut.CreateFromTemplateAsync(userId, request);

        Assert.Equal("Code Review Agent", result.Name);
        Assert.Equal("Automated code reviewer", result.Role);
        Assert.Equal("code-review", result.TemplateIdentifier);
        Assert.Equal(1, result.TemplateVersion);
        Assert.Equal(request.ProjectPrinciples, result.ProjectPrinciples);
        Assert.Null(result.CustomInstructions);
        Assert.Contains(toolAction.Id.ToString(), result.ToolActionIds);

        await _agentDataAccess.Received(1).AddAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
        await _agentToolActionDataAccess.Received(1).AssignToolActionsAsync(
            Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithNoModelOverride_UsesNullModel()
    {
        var userId = Guid.NewGuid();
        var request = CreateValidRequest();
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);
        SetupHappyPathMocks(request.WorkspaceId);

        var result = await _sut.CreateFromTemplateAsync(userId, request);

        Assert.Null(result.Model);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithNoCodeSourceIntegration_ThrowsIntegrationRequiredException()
    {
        var userId = Guid.NewGuid();
        var request = CreateValidRequest();
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);
        _availabilityResolver
            .ValidatePrerequisitesAsync(request.WorkspaceId, template, Arg.Any<CancellationToken>())
            .Throws(new IntegrationRequiredException("A Code Source integration (GitHub or GitLab) is required to deploy this template."));

        var ex = await Assert.ThrowsAsync<IntegrationRequiredException>(
            () => _sut.CreateFromTemplateAsync(userId, request));
        Assert.Contains("Code Source integration", ex.Message);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithTemplateAlreadyDeployed_ThrowsTemplateAlreadyDeployedException()
    {
        var userId = Guid.NewGuid();
        var request = CreateValidRequest();
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);
        _availabilityResolver
            .ValidatePrerequisitesAsync(request.WorkspaceId, template, Arg.Any<CancellationToken>())
            .Throws(new TemplateAlreadyDeployedException("code-review"));

        var ex = await Assert.ThrowsAsync<TemplateAlreadyDeployedException>(
            () => _sut.CreateFromTemplateAsync(userId, request));
        Assert.Contains("code-review", ex.Message);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithEmptyProjectPrinciples_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var request = new CreateAgentFromTemplateRequest(
            WorkspaceId: Guid.NewGuid(),
            TemplateId: "code-review",
            ProjectPrinciples: "",
            Model: null);
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateFromTemplateAsync(userId, request));
        Assert.Contains("Project principles", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithWhitespaceProjectPrinciples_ThrowsArgumentException()
    {
        var userId = Guid.NewGuid();
        var request = new CreateAgentFromTemplateRequest(
            WorkspaceId: Guid.NewGuid(),
            TemplateId: "code-review",
            ProjectPrinciples: "   ",
            Model: null);
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateFromTemplateAsync(userId, request));
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithUnknownTemplate_ThrowsTemplateNotFoundException()
    {
        var userId = Guid.NewGuid();
        var request = new CreateAgentFromTemplateRequest(
            WorkspaceId: Guid.NewGuid(),
            TemplateId: "nonexistent",
            ProjectPrinciples: "Some principles",
            Model: null);

        _templateRegistry.GetByIdentifier("nonexistent").Returns((BuiltInAgentTemplate?)null);

        var ex = await Assert.ThrowsAsync<TemplateNotFoundException>(
            () => _sut.CreateFromTemplateAsync(userId, request));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithBothProviders_AssignsBothToolActions()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var request = CreateValidRequest(workspaceId);
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);

        var gitHubIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.GITHUB)
            .WithType(IntegrationType.CODE_SOURCE)
            .Build();
        var gitLabIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.GITLAB)
            .WithType(IntegrationType.CODE_SOURCE)
            .Build();

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { gitHubIntegration, gitLabIntegration });

        var prToolAction = ToolAction.Create(Guid.NewGuid(), "Review Pull Request", "Reviews PRs", "review_pull_request", DangerLevel.Safe);
        var mrToolAction = ToolAction.Create(Guid.NewGuid(), "Review Merge Request", "Reviews MRs", "review_merge_request", DangerLevel.Safe);

        _toolActionDataAccess.GetByNamesAsync(
            Arg.Is<List<string>>(names => names.Contains("review_pull_request") && names.Contains("review_merge_request")),
            Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { prToolAction, mrToolAction });

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { prToolAction.Id, mrToolAction.Id });
        _agentToolActionDataAccess.GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "Code Review" });

        var result = await _sut.CreateFromTemplateAsync(userId, request);

        Assert.Equal(2, result.ToolActionIds.Length);
        await _agentToolActionDataAccess.Received(1).AssignToolActionsAsync(
            Arg.Any<Guid>(),
            Arg.Is<List<Guid>>(ids => ids.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithNonMember_ThrowsUnauthorizedWorkspaceAccessException()
    {
        var userId = Guid.NewGuid();
        var request = CreateValidRequest();

        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(userId, request.WorkspaceId, Arg.Any<CancellationToken>())
            .Throws(new UnauthorizedWorkspaceAccessException(userId, request.WorkspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.CreateFromTemplateAsync(userId, request));
    }

    [Fact]
    public async Task CreateFromTemplateAsync_WithModelOverride_SetsModelOnAgent()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var request = new CreateAgentFromTemplateRequest(
            WorkspaceId: workspaceId,
            TemplateId: "code-review",
            ProjectPrinciples: "## Standards",
            Model: "gpt-4o");
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);
        SetupHappyPathMocks(workspaceId);

        var result = await _sut.CreateFromTemplateAsync(userId, request);

        Assert.Equal("gpt-4o", result.Model);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_CallsValidatePrerequisitesBeforeCreation()
    {
        var userId = Guid.NewGuid();
        var request = CreateValidRequest();
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);
        _availabilityResolver
            .ValidatePrerequisitesAsync(request.WorkspaceId, template, Arg.Any<CancellationToken>())
            .Throws(new IntegrationRequiredException("test"));

        await Assert.ThrowsAsync<IntegrationRequiredException>(
            () => _sut.CreateFromTemplateAsync(userId, request));

        await _agentDataAccess.DidNotReceive().AddAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_ValidatesToolActionsForWorkspace()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var request = CreateValidRequest(workspaceId);
        var template = CreateCodeReviewTemplate();

        _templateRegistry.GetByIdentifier("code-review").Returns(template);
        SetupHappyPathMocks(workspaceId);

        await _sut.CreateFromTemplateAsync(userId, request);

        await _toolValidationService.Received(1).ValidateToolActionsForWorkspaceAsync(
            workspaceId, Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    private void SetupHappyPathMocks(Guid workspaceId)
    {
        var gitHubIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.GITHUB)
            .WithType(IntegrationType.CODE_SOURCE)
            .Build();

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Integration> { gitHubIntegration });

        var toolAction = ToolAction.Create(Guid.NewGuid(), "Review Pull Request", "Reviews PRs", "review_pull_request", DangerLevel.Safe);
        _toolActionDataAccess.GetByNamesAsync(Arg.Any<List<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction> { toolAction });

        _agentToolActionDataAccess.GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { toolAction.Id });
        _agentToolActionDataAccess.GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "Code Review" });
    }
}
