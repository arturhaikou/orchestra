using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Infrastructure.Tests.Integrations;

public class IntegrationResolverTests
{
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IntegrationResolver _sut;

    public IntegrationResolverTests()
    {
        _sut = new IntegrationResolver(_integrationDataAccess);
    }

    // ── FR-02 Scenario 2: Empty integrationId ────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ThrowsInvalidOperationException_WhenIntegrationIdIsEmpty()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ResolveAsync(Guid.NewGuid(), string.Empty, ProviderType.JIRA));

        Assert.Contains("integrationId is required", ex.Message);

        // Confirm: no DB call was made
        await _integrationDataAccess.DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ThrowsInvalidOperationException_WhenIntegrationIdIsWhitespace()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ResolveAsync(Guid.NewGuid(), "   ", ProviderType.GITHUB));

        Assert.Contains("integrationId is required", ex.Message);

        await _integrationDataAccess.DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── FR-02 Scenario 2: Non-GUID integrationId ─────────────────────────────

    [Fact]
    public async Task ResolveAsync_ThrowsInvalidOperationException_WhenIntegrationIdIsNotAGuid()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ResolveAsync(Guid.NewGuid(), "not-a-guid", ProviderType.JIRA));

        Assert.Contains("No active integration found for the supplied ID", ex.Message);

        await _integrationDataAccess.DidNotReceive()
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── FR-02 Scenario 5: Integration not found / inactive ───────────────────

    [Fact]
    public async Task ResolveAsync_ThrowsInvalidOperationException_WhenIntegrationNotFound()
    {
        // Arrange — GetByIdAsync returns null (not found or inactive)
        _integrationDataAccess
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Integration?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ResolveAsync(Guid.NewGuid(), Guid.NewGuid().ToString(), ProviderType.JIRA));

        Assert.Contains("No active integration found for the supplied ID", ex.Message);
    }

    // ── FR-02 Scenario 4: Workspace ownership ────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ThrowsInvalidOperationException_WhenIntegrationBelongsToDifferentWorkspace()
    {
        // Arrange — integration exists but belongs to a different workspace
        var callerWorkspaceId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithProvider(ProviderType.JIRA)
            .WithWorkspaceId(Guid.NewGuid()) // different workspace
            .Build();

        _integrationDataAccess
            .GetByIdAsync(integration.Id, Arg.Any<CancellationToken>())
            .Returns(integration);

        // Act & Assert — same not-found message to prevent cross-workspace data leakage
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ResolveAsync(callerWorkspaceId, integration.Id.ToString(), ProviderType.JIRA));

        Assert.Contains("No active integration found for the supplied ID", ex.Message);
    }

    // ── FR-02 Scenario 3: Provider type mismatch ─────────────────────────────

    [Theory]
    [InlineData(ProviderType.JIRA, "Jira")]
    [InlineData(ProviderType.GITHUB, "GitHub")]
    [InlineData(ProviderType.GITLAB, "GitLab")]
    [InlineData(ProviderType.CONFLUENCE, "Confluence")]
    public async Task ResolveAsync_ThrowsInvalidOperationException_WhenProviderTypeMismatch(
        ProviderType expectedProvider, string expectedFriendlyName)
    {
        // Arrange — integration with a different provider than requested
        var workspaceId = Guid.NewGuid();
        var wrongProvider = expectedProvider == ProviderType.JIRA ? ProviderType.GITHUB : ProviderType.JIRA;
        var integration = new IntegrationBuilder()
            .WithProvider(wrongProvider)
            .WithWorkspaceId(workspaceId)
            .Build();

        _integrationDataAccess
            .GetByIdAsync(integration.Id, Arg.Any<CancellationToken>())
            .Returns(integration);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ResolveAsync(workspaceId, integration.Id.ToString(), expectedProvider));

        Assert.Contains($"not a {expectedFriendlyName} integration", ex.Message);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ReturnsIntegration_WhenAllValidationRulesPass()
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithProvider(ProviderType.JIRA)
            .WithWorkspaceId(workspaceId)
            .Build();

        _integrationDataAccess
            .GetByIdAsync(integration.Id, Arg.Any<CancellationToken>())
            .Returns(integration);

        // Act
        var result = await _sut.ResolveAsync(workspaceId, integration.Id.ToString(), ProviderType.JIRA);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(integration.Id, result.Id);
        Assert.Equal(workspaceId, result.WorkspaceId);
        Assert.Equal(ProviderType.JIRA, result.Provider);
    }

    [Theory]
    [InlineData(ProviderType.GITHUB)]
    [InlineData(ProviderType.GITLAB)]
    [InlineData(ProviderType.CONFLUENCE)]
    public async Task ResolveAsync_ReturnsIntegration_ForAllSupportedProviders(ProviderType providerType)
    {
        // Arrange
        var workspaceId = Guid.NewGuid();
        var integration = new IntegrationBuilder()
            .WithProvider(providerType)
            .WithWorkspaceId(workspaceId)
            .Build();

        _integrationDataAccess
            .GetByIdAsync(integration.Id, Arg.Any<CancellationToken>())
            .Returns(integration);

        // Act
        var result = await _sut.ResolveAsync(workspaceId, integration.Id.ToString(), providerType);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(providerType, result.Provider);
    }
}
