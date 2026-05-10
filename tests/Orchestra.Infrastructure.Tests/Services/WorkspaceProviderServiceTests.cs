using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Services;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace Orchestra.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="WorkspaceProviderService"/>.
/// Covers BDD Scenarios 1–8 from FR-04, Phase 2.
/// </summary>
public class WorkspaceProviderServiceTests
{
    private readonly IWorkspaceAIProviderRepository _repository =
        Substitute.For<IWorkspaceAIProviderRepository>();

    private readonly IProviderCredentialEncryptionService _encryptionService =
        Substitute.For<IProviderCredentialEncryptionService>();

    private readonly IAzureOpenAIModelDiscoveryService _azureDiscovery =
        Substitute.For<IAzureOpenAIModelDiscoveryService>();

    private readonly IHttpClientFactory _httpClientFactory =
        Substitute.For<IHttpClientFactory>();

    private readonly IWorkspaceDataAccess _workspaceDataAccess =
        Substitute.For<IWorkspaceDataAccess>();

    private readonly WorkspaceProviderService _sut;

    public WorkspaceProviderServiceTests()
    {
        _sut = new WorkspaceProviderService(
            _repository,
            _encryptionService,
            _azureDiscovery,
            _httpClientFactory,
            _workspaceDataAccess);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Fake HTTP handler used to simulate Ollama responses
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal <see cref="HttpMessageHandler"/> that returns a pre-baked response.
    /// Used to avoid any real network call in unit tests.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHttpHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    /// <summary>Creates an <see cref="IHttpClientFactory"/> stub that returns an HttpClient
    /// backed by the supplied <see cref="HttpResponseMessage"/>.</summary>
    private static IHttpClientFactory BuildHttpFactory(HttpResponseMessage response)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
               .Returns(new HttpClient(new FakeHttpHandler(response)));
        return factory;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 1 — CreateProviderConfigAsync persists an Azure OpenAI config
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProviderConfigAsync_AzureOpenAI_AddsEntityWithEncryptedCredentials()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        _encryptionService.Encrypt("https://my.openai.azure.com/").Returns("ENC_ENDPOINT");
        _encryptionService.Encrypt("plaintext-api-key").Returns("ENC_APIKEY");

        // Act
        var resultId = await _sut.CreateProviderConfigAsync(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "https://my.openai.azure.com/",
            apiKey: "plaintext-api-key",
            defaultModelId: null,
            CancellationToken.None);

        // Assert — a non-empty GUID is returned
        Assert.NotEqual(Guid.Empty, resultId);

        // Assert — the repository staged exactly one entity with encrypted credentials
        await _repository.Received(1).AddAsync(
            Arg.Is<AIProviderConfiguration>(c =>
                c.WorkspaceId == workspaceId &&
                c.ProviderType == AIProviderType.AzureOpenAI &&
                c.Endpoint == "ENC_ENDPOINT" &&
                c.ApiKey == "ENC_APIKEY" &&
                c.CreatedAt != default),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 2 — CreateProviderConfigAsync persists an Ollama config
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProviderConfigAsync_Ollama_AddsEntityWithPlaintextUrl()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act
        var resultId = await _sut.CreateProviderConfigAsync(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434",
            apiKey: null,
            defaultModelId: "llama3:latest",
            CancellationToken.None);

        // Assert — a non-empty GUID is returned
        Assert.NotEqual(Guid.Empty, resultId);

        // Assert — entity has Endpoint (Ollama base URL) and DefaultModelId set; ApiKey is null
        await _repository.Received(1).AddAsync(
            Arg.Is<AIProviderConfiguration>(c =>
                c.WorkspaceId == workspaceId &&
                c.ProviderType == AIProviderType.Ollama &&
                c.Endpoint == "http://localhost:11434" &&
                c.DefaultModelId == "llama3:latest" &&
                c.ApiKey == null),
            Arg.Any<CancellationToken>());

        // Assert — encryption service was never called for Ollama
        _encryptionService.DidNotReceiveWithAnyArgs().Encrypt(default!);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 3 — CreateProviderConfigAsync throws when config already exists
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProviderConfigAsync_ConfigAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

        var existing = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "ENC_ENDPOINT",
            apiKey: "ENC_KEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.CreateProviderConfigAsync(
                workspaceId,
                AIProviderType.AzureOpenAI,
                endpoint: "https://new.openai.azure.com/",
                apiKey: "new-key",
                defaultModelId: null,
                CancellationToken.None));

        // Exception message must reference the workspace ID
        Assert.Contains(workspaceId.ToString(), ex.Message);

        // No new entity was staged
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 4 — CreateProviderConfigAsync throws on missing Azure apiKey
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProviderConfigAsync_AzureOpenAI_MissingApiKey_ThrowsArgumentException()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateProviderConfigAsync(
                workspaceId,
                AIProviderType.AzureOpenAI,
                endpoint: "https://my.openai.azure.com/",
                apiKey: null,
                defaultModelId: null,
                CancellationToken.None));

        // Exception must identify the missing parameter, must NOT echo the credential value
        Assert.Equal("apiKey", ex.ParamName);

        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateProviderConfigAsync_AzureOpenAI_MissingEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateProviderConfigAsync(
                workspaceId,
                AIProviderType.AzureOpenAI,
                endpoint: null,
                apiKey: "some-key",
                defaultModelId: null,
                CancellationToken.None));

        Assert.Equal("endpoint", ex.ParamName);

        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 5 — CreateProviderConfigAsync throws on missing Ollama URL
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProviderConfigAsync_Ollama_MissingBaseUrl_ThrowsArgumentException()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000006");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateProviderConfigAsync(
                workspaceId,
                AIProviderType.Ollama,
                endpoint: null,
                apiKey: null,
                defaultModelId: "llama3:latest",
                CancellationToken.None));

        Assert.Equal("endpoint", ex.ParamName);

        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 6 — UpdateProviderConfigAsync re-encrypts and stages the entity
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProviderConfigAsync_AzureOpenAI_ReEncryptsAndCallsUpdateAsync()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000007");

        var existingConfig = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "OLD_ENC_ENDPOINT",
            apiKey: "OLD_ENC_KEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(existingConfig);

        _encryptionService.Encrypt("https://new.openai.azure.com/").Returns("NEW_ENC_ENDPOINT");
        _encryptionService.Encrypt("new-api-key").Returns("NEW_ENC_KEY");

        // Act
        await _sut.UpdateProviderConfigAsync(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "https://new.openai.azure.com/",
            apiKey: "new-api-key",
            defaultModelId: "gpt-4o",
            CancellationToken.None);

        // Assert — UpdateAsync was called with the mutated entity including the new DefaultModelId
        await _repository.Received(1).UpdateAsync(
            Arg.Is<AIProviderConfiguration>(c =>
                c.WorkspaceId == workspaceId &&
                c.ProviderType == AIProviderType.AzureOpenAI &&
                c.Endpoint == "NEW_ENC_ENDPOINT" &&
                c.ApiKey == "NEW_ENC_KEY" &&
                c.DefaultModelId == "gpt-4o" &&
                c.UpdatedAt != null),
            Arg.Any<CancellationToken>());

        // Assert — new ciphertext differs from the original stored values
        Assert.NotEqual("OLD_ENC_ENDPOINT", existingConfig.Endpoint);
        Assert.NotEqual("OLD_ENC_KEY", existingConfig.ApiKey);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 7 — UpdateProviderConfigAsync throws when no config exists
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProviderConfigAsync_NoConfigExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000008");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.UpdateProviderConfigAsync(
                workspaceId,
                AIProviderType.AzureOpenAI,
                endpoint: "https://my.openai.azure.com/",
                apiKey: "some-key",
                defaultModelId: null,
                CancellationToken.None));

        // Exception message must reference the workspace ID
        Assert.Contains(workspaceId.ToString(), ex.Message);

        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 8: Verify UpdateProviderConfigAsync also validates missing fields
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProviderConfigAsync_AzureOpenAI_MissingApiKey_ThrowsArgumentException()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000009");

        var existingConfig = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "ENC_ENDPOINT",
            apiKey: "ENC_KEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(existingConfig);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.UpdateProviderConfigAsync(
                workspaceId,
                AIProviderType.AzureOpenAI,
                endpoint: "https://my.openai.azure.com/",
                apiKey: null,
                defaultModelId: null,
                CancellationToken.None));

        Assert.Equal("apiKey", ex.ParamName);

        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    // ── FR-018 Scenario 1: Updating provider configuration persists the new default model ID ──
    [Fact]
    public async Task UpdateProviderConfigAsync_PersistsNewDefaultModelId()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000020");

        var existingConfig = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "OLD_ENC_ENDPOINT",
            apiKey: "OLD_ENC_KEY",
            defaultModelId: "old-model");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(existingConfig);

        _encryptionService.Encrypt("https://new.openai.azure.com/").Returns("NEW_ENC_ENDPOINT");
        _encryptionService.Encrypt("new-api-key").Returns("NEW_ENC_KEY");

        // Act
        await _sut.UpdateProviderConfigAsync(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "https://new.openai.azure.com/",
            apiKey: "new-api-key",
            defaultModelId: "gpt-4o",
            CancellationToken.None);

        // Assert — DefaultModelId is updated to the new value
        await _repository.Received(1).UpdateAsync(
            Arg.Is<AIProviderConfiguration>(c =>
                c.WorkspaceId == workspaceId &&
                c.DefaultModelId == "gpt-4o"),
            Arg.Any<CancellationToken>());
    }

    // ── FR-018 Scenario 2: Switching provider clears the old model ID ────────────────────────
    [Fact]
    public async Task UpdateProviderConfigAsync_NullDefaultModelId_ClearsDefaultModelId()
    {
        // Arrange — workspace was previously configured with Ollama + a model ID
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000021");

        var existingConfig = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434",
            apiKey: null,
            defaultModelId: "llama3");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(existingConfig);

        _encryptionService.Encrypt("https://azure.openai.example.com/").Returns("ENC_ENDPOINT");
        _encryptionService.Encrypt("azure-api-key").Returns("ENC_API_KEY");

        // Act — switch to AzureOpenAI and pass null to clear the stale Ollama model ID
        await _sut.UpdateProviderConfigAsync(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "https://azure.openai.example.com/",
            apiKey: "azure-api-key",
            defaultModelId: null,
            CancellationToken.None);

        // Assert — DefaultModelId is explicitly null after the update
        await _repository.Received(1).UpdateAsync(
            Arg.Is<AIProviderConfiguration>(c =>
                c.WorkspaceId == workspaceId &&
                c.ProviderType == AIProviderType.AzureOpenAI &&
                c.DefaultModelId == null),
            Arg.Any<CancellationToken>());
    }

    // ═════════════════════════════════════════════════════════════════════
    // GetAvailableModelsAsync — BDD Scenarios from FR-06
    // ═════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 1 — Azure OpenAI: decrypts credentials and returns deployment names
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableModelsAsync_AzureOpenAI_DecryptsCredentialsAndReturnsList()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "ENC_ENDPOINT",
            apiKey: "ENC_APIKEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        _encryptionService.Decrypt("ENC_ENDPOINT").Returns("https://my.openai.azure.com/");
        _encryptionService.Decrypt("ENC_APIKEY").Returns("plain-api-key");

        _azureDiscovery
            .DiscoverModelsAsync("https://my.openai.azure.com/", "plain-api-key", Arg.Any<CancellationToken>())
            .Returns(new List<string> { "deployment-1", "deployment-2" }.AsReadOnly());

        // Act
        var result = await _sut.GetAvailableModelsAsync(workspaceId, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "deployment-1", "deployment-2" }, result);
        _encryptionService.Received(1).Decrypt("ENC_ENDPOINT");
        _encryptionService.Received(1).Decrypt("ENC_APIKEY");
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 2 — Ollama: calls /api/tags and extracts name fields
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableModelsAsync_Ollama_CallsTagsEndpointAndReturnsNames()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        const string ollamaJson = """
            {
              "models": [
                { "name": "llama3.2:latest" },
                { "name": "mistral:7b" }
              ]
            }
            """;

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ollamaJson, Encoding.UTF8, "application/json")
        };

        var sutWithFakeHttp = new WorkspaceProviderService(
            _repository,
            _encryptionService,
            _azureDiscovery,
            BuildHttpFactory(httpResponse),
            _workspaceDataAccess);

        // Act
        var result = await sutWithFakeHttp.GetAvailableModelsAsync(workspaceId, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "llama3.2:latest", "mistral:7b" }, result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 3 — Provider reachable but has no models → returns empty list
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableModelsAsync_OllamaNoModels_ReturnsEmptyList()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "models": [] }""", Encoding.UTF8, "application/json")
        };

        var sutWithFakeHttp = new WorkspaceProviderService(
            _repository,
            _encryptionService,
            _azureDiscovery,
            BuildHttpFactory(httpResponse),
            _workspaceDataAccess);

        // Act
        var result = await sutWithFakeHttp.GetAvailableModelsAsync(workspaceId, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 6 — No provider configuration → throws InvalidOperationException
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableModelsAsync_NoConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000006");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.GetAvailableModelsAsync(workspaceId, CancellationToken.None));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 7 — Provider unreachable (Azure) → throws AIProviderCommunicationException
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableModelsAsync_AzureProviderUnreachable_ThrowsAIProviderCommunicationException()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000007");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "ENC_ENDPOINT",
            apiKey: "ENC_APIKEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        _encryptionService.Decrypt("ENC_ENDPOINT").Returns("https://my.openai.azure.com/");
        _encryptionService.Decrypt("ENC_APIKEY").Returns("plain-api-key");

        _azureDiscovery
            .DiscoverModelsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>(_ =>
                throw new AIProviderCommunicationException("Failed to communicate with the Azure OpenAI provider."));

        // Act & Assert
        await Assert.ThrowsAsync<AIProviderCommunicationException>(() =>
            _sut.GetAvailableModelsAsync(workspaceId, CancellationToken.None));
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 8 — Invalid credentials (Ollama non-success response)
    //              → throws AIProviderCommunicationException
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailableModelsAsync_OllamaReturnsNonSuccessStatus_ThrowsAIProviderCommunicationException()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000008");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        // Ollama returns 503 Service Unavailable (provider is down)
        var httpResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        var sutWithFakeHttp = new WorkspaceProviderService(
            _repository,
            _encryptionService,
            _azureDiscovery,
            BuildHttpFactory(httpResponse),
            _workspaceDataAccess);

        // Act & Assert
        await Assert.ThrowsAsync<AIProviderCommunicationException>(() =>
            sutWithFakeHttp.GetAvailableModelsAsync(workspaceId, CancellationToken.None));
    }

    // ─────────────────────────────────────────────────────────────────────
    // FR-02 — ValidateProviderAsync scenarios
    // ─────────────────────────────────────────────────────────────────────

    // Scenario 1 — Valid Azure OpenAI credentials return model list

    [Fact]
    public async Task ValidateProviderAsync_AzureOpenAI_ValidCredentials_ReturnsIsValidTrueWithModels()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "ENC_ENDPOINT",
            apiKey: "ENC_APIKEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        _encryptionService.Decrypt("ENC_ENDPOINT").Returns("https://my.openai.azure.com/");
        _encryptionService.Decrypt("ENC_APIKEY").Returns("plaintext-api-key");

        _azureDiscovery
            .DiscoverModelsAsync(
                "https://my.openai.azure.com/",
                "plaintext-api-key",
                Arg.Any<CancellationToken>())
            .Returns(new List<string> { "gpt-4o", "gpt-35-turbo" }.AsReadOnly());

        var sut = new WorkspaceProviderService(
            _repository, _encryptionService, _azureDiscovery, _httpClientFactory, _workspaceDataAccess);

        // Act
        var result = await sut.ValidateProviderAsync(workspaceId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal("AzureOpenAI", result.ProviderType);
        Assert.Equal(new[] { "gpt-4o", "gpt-35-turbo" }, result.Models);
        Assert.Null(result.ErrorMessage);
    }

    // Scenario 2 — Valid Ollama configuration returns model list

    [Fact]
    public async Task ValidateProviderAsync_Ollama_ValidBaseUrl_ReturnsIsValidTrueWithModels()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        var ollamaJson = """{"models":[{"name":"llama3:8b"},{"name":"mistral:7b"}]}""";
        var httpFactory = BuildHttpFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ollamaJson, Encoding.UTF8, "application/json")
        });

        var sut = new WorkspaceProviderService(
            _repository, _encryptionService, _azureDiscovery, httpFactory, _workspaceDataAccess);

        // Act
        var result = await sut.ValidateProviderAsync(workspaceId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal("Ollama", result.ProviderType);
        Assert.Equal(new[] { "llama3:8b", "mistral:7b" }, result.Models);
        Assert.Null(result.ErrorMessage);
    }

    // Scenario 3 — Unreachable Azure OpenAI provider returns isValid: false

    [Fact]
    public async Task ValidateProviderAsync_AzureOpenAI_ProviderUnreachable_ReturnsIsValidFalseWithMessage()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "ENC_ENDPOINT",
            apiKey: "ENC_APIKEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        _encryptionService.Decrypt("ENC_ENDPOINT").Returns("https://my.openai.azure.com/");
        _encryptionService.Decrypt("ENC_APIKEY").Returns("plaintext-api-key");

        _azureDiscovery
            .DiscoverModelsAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<string>>(new AIProviderCommunicationException(
                "Failed to communicate with the Azure OpenAI provider.")));

        var sut = new WorkspaceProviderService(
            _repository, _encryptionService, _azureDiscovery, _httpClientFactory, _workspaceDataAccess);

        // Act
        var result = await sut.ValidateProviderAsync(workspaceId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal("AzureOpenAI", result.ProviderType);
        Assert.Empty(result.Models);
        Assert.NotNull(result.ErrorMessage);
        // Credential values must not appear in the error message.
        Assert.DoesNotContain("plaintext-api-key", result.ErrorMessage);
        Assert.DoesNotContain("ENC_APIKEY", result.ErrorMessage);
    }

    // Scenario 4 — Unreachable Ollama provider returns isValid: false

    [Fact]
    public async Task ValidateProviderAsync_Ollama_ProviderUnreachable_ReturnsIsValidFalseWithMessage()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000004");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        // Simulate HTTP 503 from Ollama
        var httpFactory = BuildHttpFactory(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var sut = new WorkspaceProviderService(
            _repository, _encryptionService, _azureDiscovery, httpFactory, _workspaceDataAccess);

        // Act
        var result = await sut.ValidateProviderAsync(workspaceId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal("Ollama", result.ProviderType);
        Assert.Empty(result.Models);
        Assert.NotNull(result.ErrorMessage);
    }

    // Scenario 5 — Workspace has no provider configuration → returns null

    [Fact]
    public async Task ValidateProviderAsync_NoConfiguration_ReturnsNull()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000005");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        var sut = new WorkspaceProviderService(
            _repository, _encryptionService, _azureDiscovery, _httpClientFactory, _workspaceDataAccess);

        // Act
        var result = await sut.ValidateProviderAsync(workspaceId, CancellationToken.None);

        // Assert — null signals "no config"; controller maps this to 404 Not Found.
        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario FR-03-A — CreateProviderConfigAsync stores DefaultModelId for Ollama
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProviderConfigAsync_Ollama_WithDefaultModelId_StoresDefaultModelId()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act
        await _sut.CreateProviderConfigAsync(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434",
            apiKey: null,
            defaultModelId: "llama3:latest",
            CancellationToken.None);

        // Assert — entity staged with DefaultModelId populated
        await _repository.Received(1).AddAsync(
            Arg.Is<AIProviderConfiguration>(c =>
                c.DefaultModelId == "llama3:latest" &&
                c.ProviderType == AIProviderType.Ollama),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario FR-03-B — CreateProviderConfigAsync rejects null defaultModelId for Ollama
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProviderConfigAsync_Ollama_NullDefaultModelId_ThrowsArgumentException()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateProviderConfigAsync(
                workspaceId,
                AIProviderType.Ollama,
                endpoint: "http://localhost:11434",
                apiKey: null,
                defaultModelId: null,
                CancellationToken.None));

        Assert.Equal("defaultModelId", ex.ParamName);

        // No entity staged
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────
    // FR-013: ReconfigureProviderAsync must write DefaultModelId to
    // AIProviderConfiguration only — Workspace.UpdateAsync must NOT be called
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReconfigureProviderAsync_Ollama_WritesDefaultModelIdToAIProviderConfig_NotWorkspace()
    {
        // Arrange
        var workspaceId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var newModelId = "llama3.1:latest";

        // Stub the Ollama HTTP probe to succeed and return the target model
        var ollamaResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"models\":[{{\"name\":\"{newModelId}\"}}]}}",
                System.Text.Encoding.UTF8,
                "application/json")
        };
        var fakeHandler = new FakeHttpHandler(ollamaResponse);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("http://localhost") };
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        // Existing AIProviderConfiguration record
        var existingConfig = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434",
            defaultModelId: "old-model");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(existingConfig);

        // Act
        await _sut.ReconfigureProviderAsync(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434",
            apiKey: null,
            defaultModelId: newModelId,
            CancellationToken.None);

        // Assert — AIProviderConfiguration is updated with the new DefaultModelId
        await _repository.Received(1).UpdateAsync(
            Arg.Is<AIProviderConfiguration>(c =>
                c.WorkspaceId == workspaceId &&
                c.DefaultModelId == newModelId),
            Arg.Any<CancellationToken>());

        // Assert — Workspace entity is NOT loaded or mutated
        await _workspaceDataAccess.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await _workspaceDataAccess.DidNotReceive().UpdateAsync(
            Arg.Any<Workspace>(),
            Arg.Any<CancellationToken>());

        // Assert — SaveChangesAsync is still called once to commit
        await _workspaceDataAccess.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task CreateProviderConfigAsync_Ollama_WhitespaceDefaultModelId_ThrowsArgumentException()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateProviderConfigAsync(
                workspaceId,
                AIProviderType.Ollama,
                endpoint: "http://localhost:11434",
                apiKey: null,
                defaultModelId: "   ",
                CancellationToken.None));

        Assert.Equal("defaultModelId", ex.ParamName);

        // No entity staged
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<AIProviderConfiguration>(),
            Arg.Any<CancellationToken>());
    }
}
