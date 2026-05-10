using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Integrations;

public class IntegrationServiceFr008DeleteCleanupTests
{
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IWorkspaceAuthorizationService _authService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly ICredentialEncryptionService _encryptionService = Substitute.For<ICredentialEncryptionService>();
    private readonly IMcpToolDiscoveryService _discoveryService = Substitute.For<IMcpToolDiscoveryService>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IMcpClientFactory _mcpClientFactory = Substitute.For<IMcpClientFactory>();

    private IntegrationService CreateSut() => new(
        _integrationDataAccess,
        _authService,
        _encryptionService,
        _discoveryService);

    private void SetupMcpIntegration(Guid integrationId, out Integration integration)
    {
        integration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked()
            .Build();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
    }

    private void SetupToolActionIds(Guid integrationId, IReadOnlyList<Guid> ids)
    {
        _toolActionDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(ids);
    }

    // ---------------------------------------------------------------
    // AC 1 — bulk deactivation of tool actions
    // ---------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_CallsDeactivateByIntegrationIdOnToolActions()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        await _toolActionDataAccess.Received(1)
            .DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>());
    }

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_ReturnsCorrectToolActionsDeactivatedCount()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(7);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        var result = await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        Assert.Equal(5, result.DeactivatedToolActions);
    }

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_ReturnsCorrectAgentAssignmentsRemovedCount()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(7);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        var result = await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        Assert.Equal(7, result.DeletedAgentToolActionAssignments);
    }

    // ---------------------------------------------------------------
    // AC 1 — agent assignment removal
    // ---------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_CallsDeleteByToolActionIdsWithDeactivatedIds()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var expectedIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, expectedIds);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = CreateSut();
        await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        await _agentToolActionDataAccess.Received(1)
            .DeleteByToolActionIdsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 5 && ids.All(id => expectedIds.Contains(id))),
                Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // AC 1 — tool category deactivation
    // ---------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_CallsDeactivateByIntegrationIdOnToolCategory()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        await _toolCategoryDataAccess.Received(1)
            .DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>());
    }

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_ReturnsToolCategoryDeactivatedCount_WhenCategoryDeactivated()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        var result = await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        Assert.Equal(1, result.DeactivatedToolCategories);
    }

    // ---------------------------------------------------------------
    // AC 1 — soft-delete integration
    // ---------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_CallsSoftDeleteOnIntegration()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        await _integrationDataAccess.Received(1)
            .SoftDeleteAsync(integrationId, Arg.Any<CancellationToken>());
    }

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegration_DoesNotCallUpdateAsync()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        SetupToolActionIds(integrationId, ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(0);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        await _integrationDataAccess.DidNotReceive()
            .UpdateAsync(Arg.Any<Integration>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // AC 1 — empty tools guard path
    // ---------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegrationWithNoTools_ReturnsZeroCounts()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        SetupToolActionIds(integrationId, new List<Guid>());
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = CreateSut();
        var result = await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        Assert.Equal(0, result.DeactivatedToolActions);
        Assert.Equal(0, result.DeletedAgentToolActionAssignments);
    }

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_McpIntegrationWithNoTools_SkipsDeleteByToolActionIds()
    {
        var integrationId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out _);
        SetupToolActionIds(integrationId, new List<Guid>());
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = CreateSut();
        await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        await _agentToolActionDataAccess.DidNotReceive()
            .DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Error: integration not found / not authorized
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteIntegrationAsync_IntegrationNotFound_ThrowsIntegrationNotFoundException()
    {
        var integrationId = Guid.NewGuid();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((Integration?)null);

        var sut = CreateSut();
        await Assert.ThrowsAsync<IntegrationNotFoundException>(
            () => sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId));
    }

    [Fact]
    public async Task DeleteIntegrationAsync_UserNotMember_ThrowsUnauthorizedWorkspaceAccessException()
    {
        var integrationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupMcpIntegration(integrationId, out var integration);
        _authService.EnsureUserIsMemberAsync(userId, integration.WorkspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(userId, integration.WorkspaceId));

        var sut = CreateSut();
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => sut.DeleteIntegrationAsync(userId, integrationId));
    }

    // ---------------------------------------------------------------
    // AC 3 — transport-agnostic: HTTP and stdio same cleanup path
    // ---------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_HttpMcpIntegration_AppliesSameCleanup()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked()
            .WithHttpTransport()
            .Build();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _toolActionDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(3);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        var result = await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        Assert.Equal(2, result.DeactivatedToolActions);
        Assert.Equal(3, result.DeletedAgentToolActionAssignments);
        Assert.Equal(1, result.DeactivatedToolCategories);
    }

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task DeleteIntegrationAsync_StdioMcpIntegration_AppliesSameCleanup()
    {
        var integrationId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked()
            .WithStdioTransport()
            .Build();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _toolActionDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)ids);
        _agentToolActionDataAccess.DeleteByToolActionIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(3);
        _toolCategoryDataAccess.DeactivateByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        var result = await sut.DeleteIntegrationAsync(Guid.NewGuid(), integrationId);

        Assert.Equal(2, result.DeactivatedToolActions);
        Assert.Equal(3, result.DeletedAgentToolActionAssignments);
        Assert.Equal(1, result.DeactivatedToolCategories);
    }
}
