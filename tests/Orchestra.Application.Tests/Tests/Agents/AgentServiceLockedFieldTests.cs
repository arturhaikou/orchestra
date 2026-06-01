using NSubstitute;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceLockedFieldTests
{
    private static (
        AgentService service,
        IAgentDataAccess agentDataAccess,
        IAgentToolActionDataAccess toolActionDataAccess,
        IWorkspaceAuthorizationService authService,
        IToolValidationService toolValidationService)
        BuildSut()
    {
        var agentDataAccess = Substitute.For<IAgentDataAccess>();
        var toolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
        var authService = Substitute.For<IWorkspaceAuthorizationService>();
        var toolValidationService = Substitute.For<IToolValidationService>();

        toolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        toolActionDataAccess
            .GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        toolActionDataAccess
            .ContainsReviewToolActionAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var service = new AgentService(
            agentDataAccess,
            toolActionDataAccess,
            Substitute.For<IAgentMcpToolDataAccess>(),
            Substitute.For<IAgentSubAgentDataAccess>(),
            Substitute.For<IAgentSkillDataAccess>(),
            Substitute.For<IAgentSkillFolderDataAccess>(),
            Substitute.For<ISkillDataAccess>(),
            Substitute.For<ISkillFolderDataAccess>(),
            authService,
            toolValidationService,
            Substitute.For<IBuiltInAgentTemplateRegistry>(),
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>(),
            Substitute.For<IAgentSubAgentAssignmentService>());

        return (service, agentDataAccess, toolActionDataAccess, authService, toolValidationService);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_UpdateProjectPrinciples_Succeeds()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithProjectPrinciples("Old principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: "New SOLID principles",
            Model: default);

        var result = await sut.UpdateAgentAsync(userId, agent.Id, request);

        Assert.Equal("New SOLID principles", result.ProjectPrinciples);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_UpdateModel_Succeeds()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithProjectPrinciples("Existing principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: Optional<string?>.Some("gpt-4o"));

        var result = await sut.UpdateAgentAsync(userId, agent.Id, request);

        Assert.Equal("gpt-4o", result.Model);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_ChangeName_ThrowsArgumentException()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithName("Code Reviewer")
            .WithProjectPrinciples("Principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: "My Custom Reviewer",
            Role: null,
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: default);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpdateAgentAsync(userId, agent.Id, request));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_ChangeToolActions_ThrowsArgumentException()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithProjectPrinciples("Principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: null,
            ToolActionIds: new[] { Guid.NewGuid().ToString() },
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: default);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpdateAgentAsync(userId, agent.Id, request));

        Assert.Contains("tools", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAgentAsync_CustomAgent_ChangeNameRoleAndTools_Succeeds()
    {
        var (sut, agentDataAccess, toolActionDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithName("Old Name")
            .WithRole("Old Role")
            .WithCustomInstructions("Do things")
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var toolId = Guid.NewGuid();
        var request = new UpdateAgentRequest(
            Name: "New Name",
            Role: "New Role",
            Capabilities: null,
            ToolActionIds: new[] { toolId.ToString() },
            CustomInstructions: "Do things",
            ProjectPrinciples: null,
            Model: default);

        var result = await sut.UpdateAgentAsync(userId, agent.Id, request);

        Assert.Equal("New Name", result.Name);
        Assert.Equal("New Role", result.Role);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_ChangeRole_ThrowsArgumentException()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithRole("Code Reviewer")
            .WithProjectPrinciples("Principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: "Security Auditor",
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: default);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpdateAgentAsync(userId, agent.Id, request));

        Assert.Contains("role", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_ChangeCapabilities_ThrowsArgumentException()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithCapabilities("code_review", "pull_request_analysis")
            .WithProjectPrinciples("Principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: new[] { "code_review", "security_scan" },
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: default);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpdateAgentAsync(userId, agent.Id, request));

        Assert.Contains("capabilities", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_MultipleLockedFields_ListsAllInError()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithName("Code Reviewer")
            .WithRole("Reviewer")
            .WithProjectPrinciples("Principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: "Hacked Name",
            Role: "Hacked Role",
            Capabilities: null,
            ToolActionIds: new[] { Guid.NewGuid().ToString() },
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: default);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpdateAgentAsync(userId, agent.Id, request));

        Assert.Contains("name", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tools", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_SameNameSubmitted_Succeeds()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithName("Code Reviewer")
            .WithProjectPrinciples("Principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: "Code Reviewer",
            Role: null,
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: default);

        var result = await sut.UpdateAgentAsync(userId, agent.Id, request);

        Assert.Equal("Code Reviewer", result.Name);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_SameCapabilitiesSubmitted_Succeeds()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithCapabilities("code_review", "pull_request_analysis")
            .WithProjectPrinciples("Principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: new[] { "pull_request_analysis", "code_review" },
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: null,
            Model: default);

        var result = await sut.UpdateAgentAsync(userId, agent.Id, request);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateAgentAsync_BuiltInAgent_UpdatePrinciplesAndModel_Succeeds()
    {
        var (sut, agentDataAccess, _, _, _) = BuildSut();
        var userId = Guid.NewGuid();

        var agent = new AgentBuilder()
            .WithProjectPrinciples("Old principles")
            .WithTemplateIdentifier("code-review")
            .WithTemplateVersion(1)
            .Build();

        agentDataAccess.GetByIdAsync(agent.Id, Arg.Any<CancellationToken>()).Returns(agent);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: null,
            ToolActionIds: null,
            CustomInstructions: null,
            ProjectPrinciples: "New SOLID principles",
            Model: Optional<string?>.Some("gpt-4o"));

        var result = await sut.UpdateAgentAsync(userId, agent.Id, request);

        Assert.Equal("New SOLID principles", result.ProjectPrinciples);
        Assert.Equal("gpt-4o", result.Model);
    }
}
