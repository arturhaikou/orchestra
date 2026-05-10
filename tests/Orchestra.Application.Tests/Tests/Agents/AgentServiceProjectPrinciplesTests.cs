using NSubstitute;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceProjectPrinciplesTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly Guid ReviewToolActionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid NonReviewToolActionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

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

        // Default stubs so MapToDtoAsync does not throw
        toolActionDataAccess
            .GetToolActionIdsByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        toolActionDataAccess
            .GetUniqueCategoryNamesByAgentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        // Default: no review tool present
        toolActionDataAccess
            .ContainsReviewToolActionAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var service = new AgentService(
            agentDataAccess,
            toolActionDataAccess,
            Substitute.For<IAgentMcpToolDataAccess>(),
            authService,
            toolValidationService,
            Substitute.For<IBuiltInAgentTemplateRegistry>(),
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>());

        return (service, agentDataAccess, toolActionDataAccess, authService, toolValidationService);
    }

    // -------------------------------------------------------------------------
    // Scenario 3: Successful agent creation with Project Principles
    // AC: review tool + valid projectPrinciples → agent persisted with ProjectPrinciples,
    //     CustomInstructions null, HTTP 201 (AgentDto returned).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAgentAsync_WithReviewToolAndValidProjectPrinciples_PersistsAgentWithProjectPrinciples()
    {
        // Arrange
        var (sut, agentDataAccess, toolActionDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        toolActionDataAccess
            .ContainsReviewToolActionAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(ReviewToolActionId)),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new CreateAgentRequest(
            WorkspaceId: workspaceId,
            Name: "Review Bot",
            Role: "Code Reviewer",
            Capabilities: Array.Empty<string>(),
            ToolActionIds: new[] { ReviewToolActionId.ToString() },
            CustomInstructions: null,
            ProjectPrinciples: "We follow SOLID principles; all public methods must be documented.",
            Model: null);

        Agent? capturedAgent = null;
        await agentDataAccess.AddAsync(Arg.Do<Agent>(a => capturedAgent = a), Arg.Any<CancellationToken>());

        // Act
        var result = await sut.CreateAgentAsync(userId, request);

        // Assert
        Assert.NotNull(capturedAgent);
        Assert.Equal("We follow SOLID principles; all public methods must be documented.", capturedAgent!.ProjectPrinciples);
        Assert.Null(capturedAgent.CustomInstructions);
        Assert.Equal("Review Bot", result.Name);
        Assert.Equal("We follow SOLID principles; all public methods must be documented.", result.ProjectPrinciples);
        Assert.Null(result.CustomInstructions);
    }

    // -------------------------------------------------------------------------
    // Scenario 4: Project Principles required when review tool is assigned
    // AC: review tool present + empty projectPrinciples → ArgumentException, no agent created.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAgentAsync_WithReviewToolAndEmptyProjectPrinciples_ThrowsArgumentException()
    {
        // Arrange
        var (sut, agentDataAccess, toolActionDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        toolActionDataAccess
            .ContainsReviewToolActionAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new CreateAgentRequest(
            WorkspaceId: workspaceId,
            Name: "Review Bot",
            Role: "Code Reviewer",
            Capabilities: Array.Empty<string>(),
            ToolActionIds: new[] { ReviewToolActionId.ToString() },
            CustomInstructions: null,
            ProjectPrinciples: "   ", // whitespace-only
            Model: null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreateAgentAsync(userId, request));

        await agentDataAccess.DidNotReceive().AddAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 4 (variant): Both fields provided with review tool → ArgumentException
    // AC: customInstructions non-empty AND projectPrinciples non-empty + review tool → reject.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAgentAsync_WithReviewToolAndBothFieldsProvided_ThrowsArgumentException()
    {
        // Arrange
        var (sut, agentDataAccess, toolActionDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        toolActionDataAccess
            .ContainsReviewToolActionAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new CreateAgentRequest(
            WorkspaceId: workspaceId,
            Name: "Review Bot",
            Role: "Code Reviewer",
            Capabilities: Array.Empty<string>(),
            ToolActionIds: new[] { ReviewToolActionId.ToString() },
            CustomInstructions: "Some custom instructions",
            ProjectPrinciples: "Some project principles",
            Model: null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreateAgentAsync(userId, request));

        await agentDataAccess.DidNotReceive().AddAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Custom Instructions shown when Review tool is not selected
    // AC: no review tool → CustomInstructions required; ProjectPrinciples silently discarded.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAgentAsync_WithoutReviewToolAndValidCustomInstructions_PersistsAgentWithCustomInstructions()
    {
        // Arrange
        var (sut, agentDataAccess, toolActionDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        // ContainsReviewToolActionAsync returns false (default stub)

        var request = new CreateAgentRequest(
            WorkspaceId: workspaceId,
            Name: "General Bot",
            Role: "General Agent",
            Capabilities: Array.Empty<string>(),
            ToolActionIds: new[] { NonReviewToolActionId.ToString() },
            CustomInstructions: "Help users with general tasks.",
            ProjectPrinciples: null,
            Model: null);

        Agent? capturedAgent = null;
        await agentDataAccess.AddAsync(Arg.Do<Agent>(a => capturedAgent = a), Arg.Any<CancellationToken>());

        // Act
        var result = await sut.CreateAgentAsync(userId, request);

        // Assert
        Assert.NotNull(capturedAgent);
        Assert.Equal("Help users with general tasks.", capturedAgent!.CustomInstructions);
        Assert.Null(capturedAgent.ProjectPrinciples);
        Assert.Equal("Help users with general tasks.", result.CustomInstructions);
        Assert.Null(result.ProjectPrinciples);
    }

    // -------------------------------------------------------------------------
    // Scenario 7: Project Principles value exceeds 5,000 characters
    // AC: projectPrinciples.length > 5000 → ArgumentException from domain entity
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAgentAsync_WithReviewToolAndProjectPrinciplesExceeding5000Chars_ThrowsArgumentException()
    {
        // Arrange
        var (sut, agentDataAccess, toolActionDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        toolActionDataAccess
            .ContainsReviewToolActionAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var request = new CreateAgentRequest(
            WorkspaceId: workspaceId,
            Name: "Review Bot",
            Role: "Code Reviewer",
            Capabilities: Array.Empty<string>(),
            ToolActionIds: new[] { ReviewToolActionId.ToString() },
            CustomInstructions: null,
            ProjectPrinciples: new string('A', 5001), // 5001 characters — exceeds limit
            Model: null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreateAgentAsync(userId, request));

        await agentDataAccess.DidNotReceive().AddAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 6: Field reverts to Custom Instructions when review tool is removed during edit
    // AC: ToolActionIds updated to non-review + customInstructions provided
    //     → ProjectPrinciples cleared, CustomInstructions saved.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAgentAsync_WhenReviewToolRemovedAndCustomInstructionsProvided_ClearsProjectPrinciples()
    {
        // Arrange
        var (sut, agentDataAccess, toolActionDataAccess, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var existingAgent = new AgentBuilder()
            .WithId(agentId)
            .WithProjectPrinciples("We follow SOLID principles.")
            .Build();

        agentDataAccess.GetByIdAsync(agentId, Arg.Any<CancellationToken>()).Returns(existingAgent);

        // New tool list: non-review tool only
        toolActionDataAccess
            .ContainsReviewToolActionAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(NonReviewToolActionId)),
                Arg.Any<CancellationToken>())
            .Returns(false);

        var request = new UpdateAgentRequest(
            Name: null,
            Role: null,
            Capabilities: null,
            ToolActionIds: new[] { NonReviewToolActionId.ToString() },
            CustomInstructions: "New custom instructions for general use.",
            ProjectPrinciples: null);

        Agent? savedAgent = null;
        await agentDataAccess.UpdateAsync(Arg.Do<Agent>(a => savedAgent = a), Arg.Any<CancellationToken>());

        // Act
        var result = await sut.UpdateAgentAsync(userId, agentId, request);

        // Assert
        Assert.NotNull(savedAgent);
        Assert.Equal("New custom instructions for general use.", savedAgent!.CustomInstructions);
        Assert.Null(savedAgent.ProjectPrinciples);
        Assert.Equal("New custom instructions for general use.", result.CustomInstructions);
        Assert.Null(result.ProjectPrinciples);
    }
}
