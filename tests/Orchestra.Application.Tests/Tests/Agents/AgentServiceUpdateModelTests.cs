using NSubstitute;
using Orchestra.Application.Agents.DTOs;
using Orchestra.Application.Agents.Services;
using Orchestra.Application.Common;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.McpServers.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Agents;

public class AgentServiceUpdateModelTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (
        AgentService service,
        IAgentDataAccess agentDataAccess,
        IWorkspaceAuthorizationService authService,
        IAgentToolActionDataAccess toolActionDataAccess,
        IToolValidationService toolValidationService)
        BuildSut()
    {
        var agentDataAccess = Substitute.For<IAgentDataAccess>();
        var authService = Substitute.For<IWorkspaceAuthorizationService>();
        var toolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
        var toolValidationService = Substitute.For<IToolValidationService>();

        // Default stubs so mapper doesn't throw
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
            authService,
            toolValidationService,
            Substitute.For<IBuiltInAgentTemplateRegistry>(),
            Substitute.For<ITemplateAvailabilityResolver>(),
            Substitute.For<IToolActionDataAccess>(),
            Substitute.For<IIntegrationDataAccess>(),
            Substitute.For<IAgentSubAgentAssignmentService>());

        return (service, agentDataAccess, authService, toolActionDataAccess, toolValidationService);
    }

    // -------------------------------------------------------------------------
    // Scenario 3: model field present with a new value → updates stored model
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAgentAsync_WhenModelFieldPresentWithNewValue_UpdatesStoredModel()
    {
        // Arrange
        var (sut, agentDataAccess, authService, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var existingAgent = new AgentBuilder()
            .WithId(agentId)
            .WithModel("gpt-4o")
            .Build();

        agentDataAccess
            .GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        var request = new UpdateAgentRequestBuilder()
            .WithModel("gpt-4-turbo")
            .Build();

        // Act
        var result = await sut.UpdateAgentAsync(userId, agentId, request);

        // Assert
        Assert.Equal("gpt-4-turbo", result.Model);
        await agentDataAccess.Received(1).UpdateAsync(
            Arg.Is<Agent>(a => a.Model == "gpt-4-turbo"),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 4: model field present as null → clears stored model
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAgentAsync_WhenModelFieldPresentAsNull_ClearsStoredModel()
    {
        // Arrange
        var (sut, agentDataAccess, authService, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var existingAgent = new AgentBuilder()
            .WithId(agentId)
            .WithModel("gpt-4o")
            .Build();

        agentDataAccess
            .GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        // Explicitly pass null → "Default" behaviour
        var request = new UpdateAgentRequestBuilder()
            .WithModel(null)
            .Build();

        // Act
        var result = await sut.UpdateAgentAsync(userId, agentId, request);

        // Assert
        Assert.Null(result.Model);
        await agentDataAccess.Received(1).UpdateAsync(
            Arg.Is<Agent>(a => a.Model == null),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 5: model field absent → preserved unchanged (partial update)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAgentAsync_WhenModelFieldAbsent_PreservesStoredModel()
    {
        // Arrange
        var (sut, agentDataAccess, authService, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var existingAgent = new AgentBuilder()
            .WithId(agentId)
            .WithModel("gpt-4o")
            .Build();

        agentDataAccess
            .GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        // Do NOT call .WithModel() — field is absent from the request
        var request = new UpdateAgentRequestBuilder().Build();

        // Act
        var result = await sut.UpdateAgentAsync(userId, agentId, request);

        // Assert
        Assert.Equal("gpt-4o", result.Model);
        await agentDataAccess.Received(1).UpdateAsync(
            Arg.Is<Agent>(a => a.Model == "gpt-4o"),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 6: non-member → UnauthorizedWorkspaceAccessException, no write
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAgentAsync_WhenUserIsNotMember_ThrowsUnauthorizedAndDoesNotPersist()
    {
        // Arrange
        var (sut, agentDataAccess, authService, _, _) = BuildSut();
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        var existingAgent = new AgentBuilder()
            .WithId(agentId)
            .WithModel("gpt-4o")
            .Build();

        agentDataAccess
            .GetByIdAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(existingAgent);

        authService
            .EnsureUserIsMemberAsync(userId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedWorkspaceAccessException(userId, existingAgent.WorkspaceId)));

        var request = new UpdateAgentRequestBuilder()
            .WithModel("gpt-4-turbo")
            .Build();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => sut.UpdateAgentAsync(userId, agentId, request));

        await agentDataAccess.DidNotReceive().UpdateAsync(
            Arg.Any<Agent>(),
            Arg.Any<CancellationToken>());
    }
}
