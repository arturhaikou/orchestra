using Moq;
using Orchestra.Application.AiCliIntegrations;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tests.Tests.AiCliIntegrations;

public sealed class AiCliIntegrationQueryServiceTests
{
    private readonly Mock<IWorkspaceAuthorizationService> _authMock;
    private readonly Mock<IAiCliIntegrationDataAccess> _dataAccessMock;
    private readonly AiCliIntegrationQueryService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public AiCliIntegrationQueryServiceTests()
    {
        _authMock = new Mock<IWorkspaceAuthorizationService>();
        _dataAccessMock = new Mock<IAiCliIntegrationDataAccess>();

        _authMock
            .Setup(s => s.ValidateMembershipAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new AiCliIntegrationQueryService(
            _authMock.Object,
            _dataAccessMock.Object);
    }

    // ── GetListAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_ReturnsAllWorkspaceIntegrations()
    {
        var integrations = new List<AiCliIntegration>
        {
            AiCliIntegration.Create(_workspaceId, "Copilot 1", AiCliProviderType.GITHUB_COPILOT, "encrypted:t1", false, "/path/1"),
            AiCliIntegration.Create(_workspaceId, "Copilot 2", AiCliProviderType.GITHUB_COPILOT, "encrypted:t2", false, "/path/2"),
        };

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(integrations);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Equal(2, result.Count);
        Assert.Equal("Copilot 1", result[0].Name);
        Assert.Equal("Copilot 2", result[1].Name);
    }

    [Fact]
    public async Task GetListAsync_DoesNotIncludeCredentialInDto()
    {
        var integrations = new List<AiCliIntegration>
        {
            AiCliIntegration.Create(_workspaceId, "Copilot", AiCliProviderType.GITHUB_COPILOT, "encrypted:secret", false, "/path"),
        };

        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(integrations);

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        var dto = result.Single();
        var dtoType = dto.GetType();
        Assert.False(dtoType.GetProperties().Any(p =>
            p.Name.ToLower().Contains("credential") || p.Name.ToLower().Contains("token")));
    }

    [Fact]
    public async Task GetListAsync_WhenEmpty_ReturnsEmptyList()
    {
        _dataAccessMock
            .Setup(s => s.GetByWorkspaceIdAsync(_workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AiCliIntegration>());

        var result = await _sut.GetListAsync(_userId, _workspaceId);

        Assert.Empty(result);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsMappedDto()
    {
        var integrationId = Guid.NewGuid();
        var integration = AiCliIntegration.Create(
            _workspaceId, "My Copilot", AiCliProviderType.GITHUB_COPILOT,
            "encrypted:token", false, "/workspace/project");

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(integration);

        var result = await _sut.GetByIdAsync(_userId, _workspaceId, integrationId);

        Assert.Equal("My Copilot", result.Name);
        Assert.Equal(AiCliProviderType.GITHUB_COPILOT, result.Provider);
        Assert.Equal("/workspace/project", result.WorkingDirectory);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ThrowsArgumentException()
    {
        _dataAccessMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiCliIntegration?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GetByIdAsync(_userId, _workspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdAsync_WhenBelongsToDifferentWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        var integrationId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var integration = AiCliIntegration.Create(
            otherWorkspaceId, "Foreign Copilot", AiCliProviderType.GITHUB_COPILOT,
            "encrypted:token", false, "/workspace");

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(integration);

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.GetByIdAsync(_userId, _workspaceId, integrationId));
    }
}
