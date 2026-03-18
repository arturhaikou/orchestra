using Moq;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Integrations.Services;
using Orchestra.Application.Integrations.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Interfaces;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Integrations;

public class IntegrationServiceTests
{
    private readonly Mock<IIntegrationDataAccess> _integrationDataAccessMock;
    private readonly Mock<IWorkspaceAuthorizationService> _workspaceAuthorizationServiceMock;
    private readonly Mock<ICredentialEncryptionService> _credentialEncryptionServiceMock;
    private readonly IntegrationService _sut;

    public IntegrationServiceTests()
    {
        _integrationDataAccessMock = new Mock<IIntegrationDataAccess>();
        _workspaceAuthorizationServiceMock = new Mock<IWorkspaceAuthorizationService>();
        _credentialEncryptionServiceMock = new Mock<ICredentialEncryptionService>();

        // Default stubs
        _workspaceAuthorizationServiceMock
            .Setup(s => s.ValidateMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _integrationDataAccessMock
            .Setup(s => s.ExistsByNameInWorkspaceAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _integrationDataAccessMock
            .Setup(s => s.AddAsync(It.IsAny<Integration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _credentialEncryptionServiceMock
            .Setup(s => s.Encrypt(It.IsAny<string>()))
            .Returns("encrypted_key");

        _sut = new IntegrationService(
            _integrationDataAccessMock.Object,
            _workspaceAuthorizationServiceMock.Object,
            _credentialEncryptionServiceMock.Object);
    }

    [Fact]
    public async Task CreateIntegrationAsync_WithMultipleValidTypes_ReturnsIntegrationDtoWithAllTypes()
    {
        // Arrange
        var request = new CreateIntegrationRequestBuilder()
            .WithTypes("TRACKER", "CODE_SOURCE")
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act
        var result = await _sut.CreateIntegrationAsync(Guid.NewGuid(), request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Types.Length);
        Assert.Contains("TRACKER", result.Types);
        Assert.Contains("CODE_SOURCE", result.Types);
    }

    [Fact]
    public async Task CreateIntegrationAsync_WithEmptyTypesArray_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateIntegrationRequestBuilder()
            .WithTypes()  // empty
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateIntegrationAsync(Guid.NewGuid(), request));

        Assert.Contains("At least one integration type must be selected", exception.Message);
    }

    [Fact]
    public async Task CreateIntegrationAsync_WithInvalidTypeString_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateIntegrationRequestBuilder()
            .WithTypes("INVALID_TYPE")
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateIntegrationAsync(Guid.NewGuid(), request));

        Assert.Contains("Invalid integration type: INVALID_TYPE", exception.Message);
    }

    [Fact]
    public async Task CreateIntegrationAsync_WithSingleTrackerType_PersistsAndReturnsTrackerType()
    {
        // Arrange
        var request = new CreateIntegrationRequestBuilder()
            .WithType("TRACKER")
            .WithProvider("JIRA")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("project = WEB")
            .Build();

        // Act
        var result = await _sut.CreateIntegrationAsync(Guid.NewGuid(), request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Types);
        Assert.Equal("TRACKER", result.Types[0]);
    }

    // -------------------------------------------------------------------------
    // UpdateIntegrationAsync — FR-02: Multi-Type Selector in Edit Integration Form
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateIntegrationAsync_WithNewTypesAdded_ReturnsIntegrationDtoWithAllUpdatedTypes()
    {
        // Arrange
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(Orchestra.Domain.Enums.ProviderType.GITHUB)
            .WithType(Orchestra.Domain.Enums.IntegrationType.CODE_SOURCE)
            .WithUrl("https://github.com/owner/repo")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        _integrationDataAccessMock
            .Setup(s => s.UpdateAsync(It.IsAny<Integration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateIntegrationRequestBuilder()
            .WithTypes("CODE_SOURCE", "TRACKER")
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act
        var result = await _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Types.Length);
        Assert.Contains("CODE_SOURCE", result.Types);
        Assert.Contains("TRACKER", result.Types);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_WithTypeRemoved_ReturnsIntegrationDtoWithRemainingTypeOnly()
    {
        // Arrange
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(Orchestra.Domain.Enums.ProviderType.GITHUB)
            .WithTypes([Orchestra.Domain.Enums.IntegrationType.CODE_SOURCE, Orchestra.Domain.Enums.IntegrationType.TRACKER])
            .WithUrl("https://github.com/owner/repo")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        _integrationDataAccessMock
            .Setup(s => s.UpdateAsync(It.IsAny<Integration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateIntegrationRequestBuilder()
            .WithType("CODE_SOURCE")
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act
        var result = await _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Types);
        Assert.Equal("CODE_SOURCE", result.Types[0]);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_WithEmptyTypesArray_ThrowsArgumentException()
    {
        // Arrange
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(Orchestra.Domain.Enums.ProviderType.GITHUB)
            .WithUrl("https://github.com/owner/repo")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        var request = new UpdateIntegrationRequestBuilder()
            .WithTypes()   // empty array
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request));

        Assert.Contains("At least one integration type must be selected", exception.Message);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_WithMaskedApiKeySentinel_DoesNotEncryptOrReplaceKey()
    {
        // Arrange
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(Orchestra.Domain.Enums.ProviderType.JIRA)
            .WithType(Orchestra.Domain.Enums.IntegrationType.TRACKER)
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("project = WEB")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        _integrationDataAccessMock
            .Setup(s => s.UpdateAsync(It.IsAny<Integration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateIntegrationRequestBuilder()
            .WithType("TRACKER")
            .WithProvider("JIRA")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("project = WEB")
            .WithApiKey("••••••••••••")   // masked sentinel — must NOT be re-encrypted
            .Build();

        // Act
        await _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request);

        // Assert — Encrypt must never be called when the masked sentinel is submitted
        _credentialEncryptionServiceMock.Verify(
            s => s.Encrypt(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_ForJiraProvider_ValidatesFilterQueryRegardlessOfSelectedTypes()
    {
        // Arrange — Jira integration with TRACKER type; with FR-03, type validation fires first.
        // Submitting a CODE_SOURCE type (invalid for JIRA) now throws InvalidIntegrationTypeForProviderException
        // before filter validation would have a chance to run.
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(Orchestra.Domain.Enums.ProviderType.JIRA)
            .WithType(Orchestra.Domain.Enums.IntegrationType.TRACKER)
            .WithUrl("https://example.atlassian.net")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        // Submit with CODE_SOURCE type (not TRACKER) for Jira provider — type constraint fails first
        var request = new UpdateIntegrationRequestBuilder()
            .WithType("CODE_SOURCE")
            .WithProvider("JIRA")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("AND AND")   // invalid JQL, but type validation rejects first
            .Build();

        // Act & Assert — InvalidIntegrationTypeForProviderException from FR-03 type constraint enforcement
        var exception = await Assert.ThrowsAsync<InvalidIntegrationTypeForProviderException>(
            () => _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request));

        Assert.Equal("JIRA", exception.ProviderName);
        Assert.Contains("CODE_SOURCE", exception.SubmittedTypes);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_WhenIntegrationNotFound_ThrowsIntegrationNotFoundException()
    {
        // Arrange
        var integrationId = Guid.NewGuid();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Integration?)null);   // not found

        var request = new UpdateIntegrationRequestBuilder()
            .WithType("TRACKER")
            .WithProvider("JIRA")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("project = WEB")
            .Build();

        // Act & Assert
        await Assert.ThrowsAsync<Orchestra.Application.Common.Exceptions.IntegrationNotFoundException>(
            () => _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request));
    }

    // -------------------------------------------------------------------------
    // FR-03: Provider-Driven Type Constraint Enforcement
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateIntegrationAsync_JiraWithKnowledgeBaseType_ThrowsInvalidIntegrationTypeForProviderException()
    {
        // Arrange — Jira only supports TRACKER; KNOWLEDGE_BASE must be rejected.
        var request = new CreateIntegrationRequestBuilder()
            .WithTypes("TRACKER", "KNOWLEDGE_BASE")
            .WithProvider("JIRA")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("project = WEB")
            .Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidIntegrationTypeForProviderException>(
            () => _sut.CreateIntegrationAsync(Guid.NewGuid(), request));

        Assert.Equal("JIRA", exception.ProviderName);
        Assert.Contains("KNOWLEDGE_BASE", exception.SubmittedTypes);
        Assert.DoesNotContain("KNOWLEDGE_BASE", exception.AllowedTypes);
    }

    [Fact]
    public async Task CreateIntegrationAsync_ConfluenceWithCodeSourceType_ThrowsInvalidIntegrationTypeForProviderException()
    {
        // Arrange — Confluence only supports KNOWLEDGE_BASE; CODE_SOURCE must be rejected.
        var request = new CreateIntegrationRequestBuilder()
            .WithTypes("KNOWLEDGE_BASE", "CODE_SOURCE")
            .WithProvider("CONFLUENCE")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("space = DEV")
            .Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidIntegrationTypeForProviderException>(
            () => _sut.CreateIntegrationAsync(Guid.NewGuid(), request));

        Assert.Equal("CONFLUENCE", exception.ProviderName);
        Assert.Contains("CODE_SOURCE", exception.SubmittedTypes);
    }

    [Fact]
    public async Task CreateIntegrationAsync_GitHubWithValidMultiTypeSubset_Succeeds()
    {
        // Arrange — GitHub permits any non-empty subset of {TRACKER, KNOWLEDGE_BASE, CODE_SOURCE}.
        var request = new CreateIntegrationRequestBuilder()
            .WithTypes("TRACKER", "CODE_SOURCE")
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act
        var result = await _sut.CreateIntegrationAsync(Guid.NewGuid(), request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Types.Length);
        Assert.Contains("TRACKER", result.Types);
        Assert.Contains("CODE_SOURCE", result.Types);
    }

    [Fact]
    public async Task CreateIntegrationAsync_ConfluenceWithKnowledgeBaseTypeAndMissingFilter_ThrowsArgumentException()
    {
        // Arrange — Filter validation is now provider-driven (not type-gated).
        // Even with types=[KNOWLEDGE_BASE] (not TRACKER), Confluence CQL is still required.
        var request = new CreateIntegrationRequestBuilder()
            .WithType("KNOWLEDGE_BASE")
            .WithProvider("CONFLUENCE")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery(null)   // missing CQL filter — must fail
            .Build();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateIntegrationAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task CreateIntegrationAsync_GitHubWithTrackerTypeAndNoFilter_Succeeds()
    {
        // Arrange — GitHub applies no structural filter validation regardless of types.
        var request = new CreateIntegrationRequestBuilder()
            .WithType("TRACKER")
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .WithFilterQuery(null)   // empty filter is valid for GitHub
            .Build();

        // Act
        var result = await _sut.CreateIntegrationAsync(Guid.NewGuid(), request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Types);
        Assert.Equal("TRACKER", result.Types[0]);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_JiraWithKnowledgeBaseType_ThrowsInvalidIntegrationTypeForProviderException()
    {
        // Arrange — Jira only supports TRACKER; adding KNOWLEDGE_BASE on update must be rejected.
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(ProviderType.JIRA)
            .WithType(IntegrationType.TRACKER)
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("project = WEB")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        var request = new UpdateIntegrationRequestBuilder()
            .WithTypes("TRACKER", "KNOWLEDGE_BASE")
            .WithProvider("JIRA")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("project = WEB")
            .Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidIntegrationTypeForProviderException>(
            () => _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request));

        Assert.Equal("JIRA", exception.ProviderName);
        Assert.Contains("KNOWLEDGE_BASE", exception.SubmittedTypes);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_ConfluenceWithTrackerType_ThrowsInvalidIntegrationTypeForProviderException()
    {
        // Arrange — Confluence only supports KNOWLEDGE_BASE; submitting TRACKER must be rejected.
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(ProviderType.CONFLUENCE)
            .WithType(IntegrationType.KNOWLEDGE_BASE)
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("space = DEV")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        var request = new UpdateIntegrationRequestBuilder()
            .WithTypes("KNOWLEDGE_BASE", "TRACKER")
            .WithProvider("CONFLUENCE")
            .WithUrl("https://example.atlassian.net")
            .WithFilterQuery("space = DEV")
            .Build();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidIntegrationTypeForProviderException>(
            () => _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request));

        Assert.Equal("CONFLUENCE", exception.ProviderName);
        Assert.Contains("TRACKER", exception.SubmittedTypes);
    }

    [Fact]
    public async Task UpdateIntegrationAsync_GitHubWithAllThreeTypes_Succeeds()
    {
        // Arrange — GitHub permits the full set {TRACKER, KNOWLEDGE_BASE, CODE_SOURCE}.
        var integrationId = Guid.NewGuid();
        var existingIntegration = new IntegrationBuilder()
            .WithId(integrationId)
            .WithProvider(ProviderType.GITHUB)
            .WithType(IntegrationType.TRACKER)
            .WithUrl("https://github.com/owner/repo")
            .Build();

        _integrationDataAccessMock
            .Setup(s => s.GetByIdAsync(integrationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIntegration);

        _integrationDataAccessMock
            .Setup(s => s.UpdateAsync(It.IsAny<Integration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new UpdateIntegrationRequestBuilder()
            .WithTypes("TRACKER", "KNOWLEDGE_BASE", "CODE_SOURCE")
            .WithProvider("GITHUB")
            .WithUrl("https://github.com/owner/repo")
            .Build();

        // Act
        var result = await _sut.UpdateIntegrationAsync(Guid.NewGuid(), integrationId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Types.Length);
        Assert.Contains("TRACKER", result.Types);
        Assert.Contains("KNOWLEDGE_BASE", result.Types);
        Assert.Contains("CODE_SOURCE", result.Types);
    }
}
