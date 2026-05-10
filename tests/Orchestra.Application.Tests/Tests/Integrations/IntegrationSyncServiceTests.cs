using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.Integrations.Services;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Exceptions;
using Orchestra.Domain.Interfaces;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Integrations;

public class IntegrationSyncServiceTests
{
    private readonly IIntegrationDataAccess _integrationDataAccess;
    private readonly IWorkspaceAuthorizationService _authService;
    private readonly IMcpToolDiscoveryService _mcpToolDiscoveryService;
    private readonly ICredentialEncryptionService _credentialEncryptionService;
    private readonly IntegrationService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _integrationId = Guid.NewGuid();

    public IntegrationSyncServiceTests()
    {
        _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
        _authService = Substitute.For<IWorkspaceAuthorizationService>();
        _mcpToolDiscoveryService = Substitute.For<IMcpToolDiscoveryService>();
        _credentialEncryptionService = Substitute.For<ICredentialEncryptionService>();

        _sut = new IntegrationService(
            _integrationDataAccess,
            _authService,
            _credentialEncryptionService,
            _mcpToolDiscoveryService);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: Sync discovers new tools
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenNewToolsDiscovered_ReturnsCorrectAddedCount()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .WithType(IntegrationType.MCP_SERVER)
            .Build();

        var syncResult = new SyncToolsResultDto(
            Added: 2,
            Removed: 0,
            Updated: 1,
            Total: 5,
            Tools: [
                new SyncedToolSummaryDto("new_tool_one", "added"),
                new SyncedToolSummaryDto("new_tool_two", "added"),
                new SyncedToolSummaryDto("existing_tool", "updated")
            ]);

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(syncResult);

        var result = await _sut.SyncToolsAsync(_userId, _integrationId);

        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(5, result.Total);
    }

    [Fact]
    public async Task SyncToolsAsync_WhenNewToolsDiscovered_ValidatesMembershipBeforeSync()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(new SyncToolsResultDto(0, 0, 0, 0, []));

        await _sut.SyncToolsAsync(_userId, _integrationId);

        await _authService.Received(1).ValidateMembershipAsync(
            _userId, _workspaceId, Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Sync detects removed tools
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenToolsRemoved_ReturnsCorrectRemovedCount()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        var syncResult = new SyncToolsResultDto(
            Added: 0,
            Removed: 1,
            Updated: 0,
            Total: 4,
            Tools: [new SyncedToolSummaryDto("removed_tool", "removed")]);

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(syncResult);

        var result = await _sut.SyncToolsAsync(_userId, _integrationId);

        Assert.Equal(1, result.Removed);
        Assert.Equal(4, result.Total);
        Assert.Contains(result.Tools, t => t.Status == "removed");
    }

    // -------------------------------------------------------------------------
    // Scenario 3: Sync on non-MCP integration rejected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenIntegrationIsNotMcpBacked_ThrowsInvalidOperationException()
    {
        var nonMcpIntegration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(false)
            .WithType(IntegrationType.TRACKER)
            .WithProvider(ProviderType.JIRA)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(nonMcpIntegration);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));

        Assert.Contains("not MCP-backed", ex.Message);
    }

    [Fact]
    public async Task SyncToolsAsync_WhenIntegrationIsNotMcpBacked_DoesNotCallDiscoveryService()
    {
        var nonMcpIntegration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(false)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(nonMcpIntegration);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));

        await _mcpToolDiscoveryService.DidNotReceive()
            .SyncToolsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 4: Integration not found
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenIntegrationNotFound_ThrowsIntegrationNotFoundException()
    {
        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns((Orchestra.Domain.Entities.Integration?)null);

        await Assert.ThrowsAsync<IntegrationNotFoundException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));
    }

    // -------------------------------------------------------------------------
    // Auth guard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenUserNotWorkspaceMember_ThrowsUnauthorized()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _authService.ValidateMembershipAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedWorkspaceAccessException(_userId, _workspaceId));

        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));
    }

    // -------------------------------------------------------------------------
    // MCP server error propagation (Scenario 4)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenMcpServerUnreachable_PropagatesMcpConnectionException()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpConnectionException(
                McpConnectionErrorCode.MCP_UNREACHABLE,
                "MCP server is unreachable."));

        await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));
    }

    [Fact]
    public async Task SyncToolsAsync_WhenMcpServerUnreachable_NoToolRecordsModified()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpConnectionException(McpConnectionErrorCode.MCP_UNREACHABLE, "Unreachable"));

        await Assert.ThrowsAsync<McpConnectionException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));

        await _integrationDataAccess.DidNotReceive().UpdateAsync(
            Arg.Any<Orchestra.Domain.Entities.Integration>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // AC3 — stdio process launch failure propagation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenStdioProcessFailsToStart_PropagatesMcpProcessStartException()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpProcessStartException("npx", "Command not found"));

        await Assert.ThrowsAsync<McpProcessStartException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));
    }

    [Fact]
    public async Task SyncToolsAsync_WhenStdioProcessFailsToStart_ToolCatalogueUnchanged()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpProcessStartException("npx", "Command not found"));

        await Assert.ThrowsAsync<McpProcessStartException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));

        await _integrationDataAccess.DidNotReceive().UpdateAsync(
            Arg.Any<Orchestra.Domain.Entities.Integration>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // AC3 — discovery timeout propagation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WhenDiscoveryTimesOut_PropagatesMcpDiscoveryTimeoutException()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpDiscoveryTimeoutException(_integrationId, TimeSpan.FromSeconds(30)));

        await Assert.ThrowsAsync<McpDiscoveryTimeoutException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));
    }

    [Fact]
    public async Task SyncToolsAsync_WhenDiscoveryTimesOut_ToolCatalogueUnchanged()
    {
        var integration = new IntegrationBuilder()
            .WithId(_integrationId)
            .WithWorkspaceId(_workspaceId)
            .WithIsMcpBacked(true)
            .Build();

        _integrationDataAccess.GetByIdAsync(_integrationId, Arg.Any<CancellationToken>())
            .Returns(integration);
        _mcpToolDiscoveryService.SyncToolsAsync(_integrationId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new McpDiscoveryTimeoutException(_integrationId, TimeSpan.FromSeconds(30)));

        await Assert.ThrowsAsync<McpDiscoveryTimeoutException>(
            () => _sut.SyncToolsAsync(_userId, _integrationId));

        await _integrationDataAccess.DidNotReceive().UpdateAsync(
            Arg.Any<Orchestra.Domain.Entities.Integration>(), Arg.Any<CancellationToken>());
    }
}
