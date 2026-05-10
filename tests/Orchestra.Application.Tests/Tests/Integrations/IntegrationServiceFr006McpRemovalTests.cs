using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Application.Integrations.Services;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Integrations;

public class IntegrationServiceFr006McpRemovalTests
{
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IWorkspaceAuthorizationService _authService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly ICredentialEncryptionService _encryptionService = Substitute.For<ICredentialEncryptionService>();
    private readonly IMcpToolDiscoveryService _discoveryService = Substitute.For<IMcpToolDiscoveryService>();

    private IntegrationService CreateSut() => new(
        _integrationDataAccess,
        _authService,
        _encryptionService,
        _discoveryService);

    // ---------------------------------------------------------------
    // Scenario 1: Integration list no longer returns MCP entries
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetWorkspaceIntegrationsAsync_WhenWorkspaceContainsMcpIntegration_ExcludesMcpFromResult()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var mcpIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .AsMcpBacked()
            .Build();

        var jiraIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.JIRA)
            .Build();

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns([mcpIntegration, jiraIntegration]);

        var sut = CreateSut();

        var result = await sut.GetWorkspaceIntegrationsAsync(userId, workspaceId);

        Assert.Single(result);
        Assert.Equal(ProviderType.JIRA.ToString(), result[0].Provider);
    }

    [Fact]
    public async Task GetWorkspaceIntegrationsAsync_WhenWorkspaceHasOnlyNativeIntegrations_ReturnsAll()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var jiraIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.JIRA)
            .Build();

        var githubIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.GITHUB)
            .Build();

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns([jiraIntegration, githubIntegration]);

        var sut = CreateSut();

        var result = await sut.GetWorkspaceIntegrationsAsync(userId, workspaceId);

        Assert.Equal(2, result.Count);
    }

    // ---------------------------------------------------------------
    // Scenario 5: Create integration rejects MCP_GENERIC provider
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateIntegrationAsync_WithMcpGenericProvider_ThrowsArgumentExceptionWithExpectedMessage()
    {
        var request = new CreateIntegrationRequestBuilder()
            .WithProvider("MCP_GENERIC")
            .WithTypes("TRACKER")
            .WithWorkspaceId(Guid.NewGuid())
            .WithName("My MCP Server")
            .Build();

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => sut.CreateIntegrationAsync(Guid.NewGuid(), request));

        Assert.Contains("MCP servers must be managed through the MCP Server settings", ex.Message);
    }

    // ---------------------------------------------------------------
    // Scenario 4: Non-MCP integrations unaffected
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateIntegrationAsync_WithJiraProvider_DoesNotThrow()
    {
        var workspaceId = Guid.NewGuid();
        var request = new CreateIntegrationRequestBuilder()
            .WithProvider("JIRA")
            .WithTypes("TRACKER")
            .WithWorkspaceId(workspaceId)
            .WithName("Jira Integration")
            .WithUrl("https://myorg.atlassian.net")
            .WithFilterQuery("project = TEST")
            .Build();

        _integrationDataAccess.ExistsByNameInWorkspaceAsync(Arg.Any<string>(), workspaceId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _integrationDataAccess.ExistsByProviderInWorkspaceAsync(ProviderType.JIRA, workspaceId, Arg.Any<CancellationToken>())
            .Returns(false);
        _integrationDataAccess.AddAsync(Arg.Any<Domain.Entities.Integration>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _encryptionService.Encrypt(Arg.Any<string>()).Returns("encrypted");

        var sut = CreateSut();

        var result = await sut.CreateIntegrationAsync(Guid.NewGuid(), request);

        Assert.NotNull(result);
        Assert.Equal("JIRA", result.Provider);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_WithMcpGenericProvider_ThrowsArgumentException()
    {
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(ProviderType.JIRA)
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(existingIntegration);

        var request = new UpdateIntegrationRequestBuilder()
            .WithProvider("MCP_GENERIC")
            .WithTypes("TRACKER")
            .WithName("Updated Name")
            .Build();

        var sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request));
    }

    // ---------------------------------------------------------------
    // SyncToolsAsync guard: rejects MCP_GENERIC
    // ---------------------------------------------------------------

    [Fact]
    public async Task SyncToolsAsync_WithMcpGenericIntegration_ThrowsInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var integrationId = Guid.NewGuid();

        var mcpIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .AsMcpBacked()
            .Build();

        _integrationDataAccess.GetByIdAsync(integrationId, Arg.Any<CancellationToken>())
            .Returns(mcpIntegration);

        var sut = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.SyncToolsAsync(userId, integrationId));

        Assert.Contains("MCP servers must be managed through the MCP Server settings", ex.Message);
    }
}
