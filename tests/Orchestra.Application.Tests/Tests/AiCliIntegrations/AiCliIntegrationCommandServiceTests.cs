using Moq;
using Orchestra.Application.AiCliIntegrations;
using Orchestra.Application.AiCliIntegrations.DTOs;
using Orchestra.Application.AiCliIntegrations.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Application.Tests.Tests.AiCliIntegrations;

public sealed class AiCliIntegrationCommandServiceTests
{
    private readonly Mock<IWorkspaceAuthorizationService> _authMock;
    private readonly Mock<IAiCliIntegrationDataAccess> _dataAccessMock;
    private readonly Mock<ICredentialEncryptionService> _encryptionMock;
    private readonly AiCliIntegrationCommandService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public AiCliIntegrationCommandServiceTests()
    {
        _authMock = new Mock<IWorkspaceAuthorizationService>();
        _dataAccessMock = new Mock<IAiCliIntegrationDataAccess>();
        _encryptionMock = new Mock<ICredentialEncryptionService>();

        _authMock
            .Setup(s => s.ValidateMembershipAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _encryptionMock
            .Setup(s => s.Encrypt(It.IsAny<string>()))
            .Returns((string p) => $"encrypted:{p}");

        _sut = new AiCliIntegrationCommandService(
            _authMock.Object,
            _dataAccessMock.Object,
            _encryptionMock.Object);
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesIntegrationAndReturnsDto()
    {
        var request = new CreateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "My Copilot",
            Provider: AiCliProviderType.GITHUB_COPILOT,
            Credential: "ghp_token",
            UseLoggedInUser: false,
            WorkingDirectory: "/workspace/project");

        var result = await _sut.CreateAsync(_userId, request);

        Assert.NotNull(result);
        Assert.Equal("My Copilot", result.Name);
        Assert.Equal(AiCliProviderType.GITHUB_COPILOT, result.Provider);
        Assert.Equal("/workspace/project", result.WorkingDirectory);
        Assert.False(result.UseLoggedInUser);
        _dataAccessMock.Verify(
            s => s.AddAsync(It.IsAny<AiCliIntegration>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithUseLoggedInUser_DoesNotEncryptCredential()
    {
        var request = new CreateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "Logged In Copilot",
            Provider: AiCliProviderType.GITHUB_COPILOT,
            Credential: null,
            UseLoggedInUser: true,
            WorkingDirectory: "/workspace");

        var result = await _sut.CreateAsync(_userId, request);

        Assert.True(result.UseLoggedInUser);
        _encryptionMock.Verify(s => s.Encrypt(It.IsAny<string>()), Times.Never);
        _dataAccessMock.Verify(
            s => s.AddAsync(It.IsAny<AiCliIntegration>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_EncryptsCredentialBeforeStorage()
    {
        var request = new CreateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "Secured Copilot",
            Provider: AiCliProviderType.GITHUB_COPILOT,
            Credential: "ghp_secret",
            UseLoggedInUser: false,
            WorkingDirectory: "/workspace");

        await _sut.CreateAsync(_userId, request);

        _encryptionMock.Verify(s => s.Encrypt("ghp_secret"), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DoesNotExposeCredentialInDto()
    {
        var request = new CreateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "Safe Copilot",
            Provider: AiCliProviderType.GITHUB_COPILOT,
            Credential: "ghp_secret",
            UseLoggedInUser: false,
            WorkingDirectory: "/workspace");

        var result = await _sut.CreateAsync(_userId, request);

        var dtoType = result.GetType();
        Assert.False(dtoType.GetProperties().Any(p => p.Name.ToLower().Contains("credential") || p.Name.ToLower().Contains("token")));
    }

    [Fact]
    public async Task CreateAsync_WhenNameAlreadyExists_ThrowsValidationException()
    {
        _dataAccessMock
            .Setup(s => s.ExistsByNameAsync(_workspaceId, "Duplicate", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new CreateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "Duplicate",
            Provider: AiCliProviderType.GITHUB_COPILOT,
            Credential: "ghp_token",
            UseLoggedInUser: false,
            WorkingDirectory: "/workspace");

        await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateAsync(_userId, request));
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesAndReturnsDto()
    {
        var integrationId = Guid.NewGuid();
        var existing = AiCliIntegration.Create(
            _workspaceId, "Old Name", AiCliProviderType.GITHUB_COPILOT,
            "encrypted:old", false, "/old/path");

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "New Name",
            Credential: "ghp_new",
            UseLoggedInUser: false,
            WorkingDirectory: "/new/path");

        var result = await _sut.UpdateAsync(_userId, integrationId, request);

        Assert.Equal("New Name", result.Name);
        Assert.Equal("/new/path", result.WorkingDirectory);
        _dataAccessMock.Verify(
            s => s.UpdateAsync(It.IsAny<AiCliIntegration>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenSwitchingToUseLoggedInUser_ClearsStoredCredential()
    {
        var integrationId = Guid.NewGuid();
        var existing = AiCliIntegration.Create(
            _workspaceId, "Copilot", AiCliProviderType.GITHUB_COPILOT,
            "encrypted:old_token", false, "/workspace");

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "Copilot",
            Credential: null,
            UseLoggedInUser: true,
            WorkingDirectory: "/workspace");

        await _sut.UpdateAsync(_userId, integrationId, request);

        _encryptionMock.Verify(s => s.Encrypt(It.IsAny<string>()), Times.Never);
        _dataAccessMock.Verify(
            s => s.UpdateAsync(
                It.Is<AiCliIntegration>(a => a.EncryptedCredential == null && a.UseLoggedInUser),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenCredentialIsNull_PreservesExistingEncryptedCredential()
    {
        var integrationId = Guid.NewGuid();
        var existing = AiCliIntegration.Create(
            _workspaceId, "Copilot", AiCliProviderType.GITHUB_COPILOT,
            "encrypted:original_token", false, "/workspace");

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var request = new UpdateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "Copilot",
            Credential: null,
            UseLoggedInUser: false,
            WorkingDirectory: "/workspace");

        await _sut.UpdateAsync(_userId, integrationId, request);

        _encryptionMock.Verify(s => s.Encrypt(It.IsAny<string>()), Times.Never);
        _dataAccessMock.Verify(
            s => s.UpdateAsync(
                It.Is<AiCliIntegration>(a => a.EncryptedCredential == "encrypted:original_token"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenIntegrationNotFound_ThrowsArgumentException()
    {
        _dataAccessMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiCliIntegration?)null);

        var request = new UpdateAiCliIntegrationRequest(
            WorkspaceId: _workspaceId,
            Name: "Name",
            Credential: null,
            UseLoggedInUser: false,
            WorkingDirectory: "/workspace");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateAsync(_userId, Guid.NewGuid(), request));
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WithValidRequest_DeletesIntegration()
    {
        var integrationId = Guid.NewGuid();
        var existing = AiCliIntegration.Create(
            _workspaceId, "Copilot", AiCliProviderType.GITHUB_COPILOT,
            "encrypted:token", false, "/workspace");

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _sut.DeleteAsync(_userId, integrationId, _workspaceId);

        _dataAccessMock.Verify(
            s => s.DeleteAsync(It.IsAny<AiCliIntegration>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenIntegrationNotFound_ThrowsArgumentException()
    {
        _dataAccessMock
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiCliIntegration?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.DeleteAsync(_userId, Guid.NewGuid(), _workspaceId));
    }

    [Fact]
    public async Task DeleteAsync_WhenIntegrationBelongsToDifferentWorkspace_ThrowsWorkspaceAccessDeniedException()
    {
        var integrationId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var existing = AiCliIntegration.Create(
            otherWorkspaceId, "Copilot", AiCliProviderType.GITHUB_COPILOT,
            "encrypted:token", false, "/workspace");

        _dataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(
            () => _sut.DeleteAsync(_userId, integrationId, _workspaceId));
    }
}
