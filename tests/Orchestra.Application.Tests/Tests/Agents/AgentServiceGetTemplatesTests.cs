using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Agents.Templates;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceGetTemplatesTests
{
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IWorkspaceAuthorizationService _authService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly IToolValidationService _toolValidationService = Substitute.For<IToolValidationService>();
    private readonly IBuiltInAgentTemplateRegistry _templateRegistry = Substitute.For<IBuiltInAgentTemplateRegistry>();
    private readonly ITemplateAvailabilityResolver _availabilityResolver = Substitute.For<ITemplateAvailabilityResolver>();
    private readonly AgentService _sut;

    public AgentServiceGetTemplatesTests()
    {
        _sut = new AgentService(
            _agentDataAccess,
            _agentToolActionDataAccess,
            Substitute.For<IAgentMcpToolDataAccess>(),
            Substitute.For<IAgentSubAgentDataAccess>(),
            Substitute.For<IAgentSkillDataAccess>(),
            Substitute.For<ISkillDataAccess>(),
            _authService,
            _toolValidationService,
            _templateRegistry,
            _availabilityResolver,
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>(),
            Substitute.For<IAgentSubAgentAssignmentService>());
    }

    [Fact]
    public async Task GetAgentTemplatesAsync_WithValidMember_ReturnsTemplatesWithAvailability()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        _availabilityResolver.ResolveAvailabilityAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<ResolvedTemplate>
            {
                new(
                    TemplateId: "code-review",
                    Status: TemplateAvailabilityStatus.Available,
                    UnavailabilityReason: null,
                    ExistingAgentId: null,
                    ResolvedToolActions: new List<ResolvedToolAction>
                    {
                        new(Guid.NewGuid(), "review_pull_request", ProviderType.GITHUB)
                    },
                    ProviderLabels: new List<ProviderLabel>
                    {
                        new(ProviderType.GITHUB, "Pull Request")
                    },
                    ResolvedGuide: "Create a ticket and provide a Pull Request link.")
            });

        var results = await _sut.GetAgentTemplatesAsync(userId, workspaceId);

        Assert.Single(results);
        var dto = results[0];
        Assert.Equal("code-review", dto.TemplateId);
        Assert.Equal("AVAILABLE", dto.Availability.Status);
        Assert.Null(dto.Availability.Reason);
        Assert.Null(dto.Availability.ExistingAgentId);
        Assert.Single(dto.Prerequisites);
        Assert.Equal("GITHUB", dto.Prerequisites[0].IntegrationType);
        Assert.Equal("Pull Request", dto.Prerequisites[0].ProviderName);
    }

    [Fact]
    public async Task GetAgentTemplatesAsync_CallsEnsureUserIsMember()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        _availabilityResolver.ResolveAvailabilityAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<ResolvedTemplate>());

        await _sut.GetAgentTemplatesAsync(userId, workspaceId);

        await _authService.Received(1).EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAgentTemplatesAsync_WithDeployedTemplate_ReturnsDeployedStatus()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var deployedAgentId = Guid.NewGuid();

        _availabilityResolver.ResolveAvailabilityAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<ResolvedTemplate>
            {
                new(
                    TemplateId: "code-review",
                    Status: TemplateAvailabilityStatus.AlreadyDeployed,
                    UnavailabilityReason: null,
                    ExistingAgentId: deployedAgentId,
                    ResolvedToolActions: new List<ResolvedToolAction>
                    {
                        new(Guid.NewGuid(), "review_pull_request", ProviderType.GITHUB)
                    },
                    ProviderLabels: new List<ProviderLabel>
                    {
                        new(ProviderType.GITHUB, "Pull Request")
                    },
                    ResolvedGuide: "Create a ticket and provide a Pull Request link.")
            });

        var results = await _sut.GetAgentTemplatesAsync(userId, workspaceId);

        Assert.Equal("ALREADY_DEPLOYED", results[0].Availability.Status);
        Assert.Equal(deployedAgentId, results[0].Availability.ExistingAgentId);
    }

    [Fact]
    public async Task GetAgentTemplatesAsync_WithNonMember_ThrowsUnauthorizedWorkspaceAccessException()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        _authService.EnsureUserIsMemberAsync(userId, workspaceId, Arg.Any<CancellationToken>())
.ThrowsAsync(new UnauthorizedWorkspaceAccessException(userId, workspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.GetAgentTemplatesAsync(userId, workspaceId));
    }

    [Fact]
    public async Task GetAgentTemplatesAsync_WithUnavailableTemplate_ReturnsUnavailabilityReason()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var reason = "Requires a Code Source integration (GitHub or GitLab). Configure one in Settings → Integrations.";

        _availabilityResolver.ResolveAvailabilityAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<ResolvedTemplate>
            {
                new(
                    TemplateId: "code-review",
                    Status: TemplateAvailabilityStatus.Unavailable,
                    UnavailabilityReason: reason,
                    ExistingAgentId: null,
                    ResolvedToolActions: new List<ResolvedToolAction>(),
                    ProviderLabels: new List<ProviderLabel>(),
                    ResolvedGuide: "generic guide text")
            });

        var results = await _sut.GetAgentTemplatesAsync(userId, workspaceId);

        Assert.Equal("UNAVAILABLE", results[0].Availability.Status);
        Assert.Equal(reason, results[0].Availability.Reason);
    }

    [Fact]
    public async Task GetAgentTemplatesAsync_WithEmptyRegistry_ReturnsEmptyList()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        _availabilityResolver.ResolveAvailabilityAsync(userId, workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<ResolvedTemplate>());

        var results = await _sut.GetAgentTemplatesAsync(userId, workspaceId);

        Assert.Empty(results);
    }
}
