using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Integrations.Services;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.Tests.Tests.Integrations;

public class IntegrationServiceGetDeletionImpactTests
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

    // ----------------------------------------------------------------
    // MCP integration — full impact
    // ----------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task GetDeletionImpactAsync_McpIntegration_ReturnsCounts()
    {
        var integrationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integration = new IntegrationBuilder().WithId(integrationId).AsMcpBacked().Build();
        var toolActions = Enumerable.Range(0, 10)
            .Select(_ => new ToolActionBuilder().AsMcpTool(integrationId).Build())
            .ToList();
        var category = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _toolActionDataAccess.GetActiveByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(toolActions);
        _agentToolActionDataAccess.CountByToolActionIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>()).Returns(3);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(category);
        _toolActionDataAccess.GetByCategoryIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(toolActions);

        var sut = CreateSut();
        var result = await sut.GetDeletionImpactAsync(userId, integrationId);

        Assert.Equal(10, result.ToolActionsToDeactivate);
        Assert.Equal(3, result.AgentAssignmentsToRemove);
        Assert.True(result.ToolCategoryWillDeactivate);
    }

    [Fact]
    public async Task GetDeletionImpactAsync_McpIntegration_CategoryHasOtherTools_CategoryWillNotDeactivate()
    {
        var integrationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integration = new IntegrationBuilder().WithId(integrationId).AsMcpBacked().Build();
        var mcpTools = new List<ToolAction> { new ToolActionBuilder().AsMcpTool(integrationId).Build() };
        var category = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();
        var allCategoryTools = new List<ToolAction>
        {
            mcpTools[0],
            new ToolActionBuilder().Build()
        };

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _toolActionDataAccess.GetActiveByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(mcpTools);
        _agentToolActionDataAccess.CountByToolActionIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>()).Returns(0);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(category);
        _toolActionDataAccess.GetByCategoryIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(allCategoryTools);

        var sut = CreateSut();
        var result = await sut.GetDeletionImpactAsync(userId, integrationId);

        Assert.False(result.ToolCategoryWillDeactivate);
    }

    // ----------------------------------------------------------------
    // Non-MCP integration — zeroed impact
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetDeletionImpactAsync_NonMcpIntegration_ReturnsZeroedImpact()
    {
        var integrationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integration = new IntegrationBuilder().WithId(integrationId).Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);

        var sut = CreateSut();
        var result = await sut.GetDeletionImpactAsync(userId, integrationId);

        Assert.Equal(0, result.ToolActionsToDeactivate);
        Assert.Equal(0, result.AgentAssignmentsToRemove);
        Assert.False(result.ToolCategoryWillDeactivate);
    }

    // ----------------------------------------------------------------
    // Integration not found
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetDeletionImpactAsync_IntegrationNotFound_ThrowsIntegrationNotFoundException()
    {
        var integrationId = Guid.NewGuid();
        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns((Integration?)null);

        var sut = CreateSut();
        await Assert.ThrowsAsync<IntegrationNotFoundException>(
            () => sut.GetDeletionImpactAsync(Guid.NewGuid(), integrationId));
    }

    // ----------------------------------------------------------------
    // MCP integration — zero tool actions (edge case)
    // ----------------------------------------------------------------

    [Fact(Skip = "FR-006: MCP cleanup path removed")]
    public async Task GetDeletionImpactAsync_McpIntegrationWithNoActiveTools_ReturnsZeroCounts()
    {
        var integrationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var integration = new IntegrationBuilder().WithId(integrationId).AsMcpBacked().Build();
        var category = new ToolCategoryBuilder().WithIntegrationId(integrationId).Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(integration);
        _toolActionDataAccess.GetActiveByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());
        _agentToolActionDataAccess.CountByToolActionIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>()).Returns(0);
        _toolCategoryDataAccess.FindByIntegrationIdAsync(integrationId, Arg.Any<CancellationToken>()).Returns(category);
        _toolActionDataAccess.GetByCategoryIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(new List<ToolAction>());

        var sut = CreateSut();
        var result = await sut.GetDeletionImpactAsync(userId, integrationId);

        Assert.Equal(0, result.ToolActionsToDeactivate);
        Assert.Equal(0, result.AgentAssignmentsToRemove);
        Assert.True(result.ToolCategoryWillDeactivate);
    }
}
