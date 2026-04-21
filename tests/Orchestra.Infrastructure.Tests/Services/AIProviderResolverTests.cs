using Microsoft.Extensions.AI;
using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Services;
using Xunit;

namespace Orchestra.Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AIProviderResolver"/>.
/// Covers BDD Scenarios 1–3 from FR-03 and verifies the single-query NFR.
/// </summary>
public class AIProviderResolverTests
{
    private readonly IWorkspaceAIProviderRepository _repository =
        Substitute.For<IWorkspaceAIProviderRepository>();

    private readonly IProviderCredentialEncryptionService _encryptionService =
        Substitute.For<IProviderCredentialEncryptionService>();

    private readonly AIProviderResolver _sut;

    public AIProviderResolverTests()
    {
        _sut = new AIProviderResolver(_repository, _encryptionService);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Scenario 1 — Azure OpenAI workspace returns a non-null IChatClient
    // and the encryption service is called exactly twice (endpoint + key).
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_AzureOpenAI_ReturnsNonNullIChatClient()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "ENCRYPTED_ENDPOINT",
            apiKey: "ENCRYPTED_KEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        // Return a syntactically valid (fake) endpoint and key so AzureOpenAIClient construction succeeds.
        _encryptionService.Decrypt("ENCRYPTED_ENDPOINT").Returns("https://fake.openai.azure.com/");
        _encryptionService.Decrypt("ENCRYPTED_KEY").Returns("fake-api-key-00000000000000000000000000000000");

        // Act — modelId is passed as a parameter, not stored on the entity.
        var result = await _sut.ResolveAsync(workspaceId, "gpt-4o", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IChatClient>(result);
    }

    [Fact]
    public async Task ResolveAsync_AzureOpenAI_DecryptsEndpointAndApiKey()
    {
        // Arrange
        var workspaceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "CIPHER_ENDPOINT",
            apiKey: "CIPHER_KEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        _encryptionService.Decrypt("CIPHER_ENDPOINT").Returns("https://fake.openai.azure.com/");
        _encryptionService.Decrypt("CIPHER_KEY").Returns("fake-api-key-00000000000000000000000000000000");

        // Act — modelId is passed as a parameter, not stored on the entity.
        await _sut.ResolveAsync(workspaceId, "gpt-4o", CancellationToken.None);

        // Assert — decryption was invoked exactly once per credential, never more.
        _encryptionService.Received(1).Decrypt("CIPHER_ENDPOINT");
        _encryptionService.Received(1).Decrypt("CIPHER_KEY");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 2 — Ollama workspace returns a non-null IChatClient
    // and the encryption service is NOT called (OllamaBaseUrl is plaintext).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_Ollama_ReturnsNonNullIChatClient()
    {
        // Arrange
        var workspaceId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.Ollama,
            endpoint: "http://localhost:11434");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        // Act — modelId specifies the Ollama model tag baked into the client.
        var result = await _sut.ResolveAsync(workspaceId, "llama3.1", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IChatClient>(result);
    }

    [Fact]
    public async Task ResolveAsync_Ollama_DoesNotCallDecrypt()
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

        // Act — modelId specifies the Ollama model tag baked into the client.
        await _sut.ResolveAsync(workspaceId, "llama3.1", CancellationToken.None);

        // Assert — plaintext base URL: no decryption should occur.
        _encryptionService.DidNotReceive().Decrypt(Arg.Any<string>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 3 — No configuration in the repository throws InvalidOperationException
    // with a message that includes the workspace ID.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_NullConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var workspaceId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ResolveAsync(workspaceId, "gpt-4o", CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_NullConfig_ExceptionMessageContainsWorkspaceId()
    {
        // Arrange
        var workspaceId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);

        // Act
        var ex = await Record.ExceptionAsync(
            () => _sut.ResolveAsync(workspaceId, "gpt-4o", CancellationToken.None));

        // Assert — message is developer-actionable and references the workspace ID.
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains(workspaceId.ToString(), ex.Message);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NFR — the resolver issues exactly ONE database query per invocation.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_AzureOpenAI_CallsRepositoryExactlyOnce()
    {
        // Arrange
        var workspaceId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

        var config = AIProviderConfiguration.Create(
            workspaceId,
            AIProviderType.AzureOpenAI,
            endpoint: "CIPHER_ENDPOINT",
            apiKey: "CIPHER_KEY");

        _repository
            .GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(config);

        _encryptionService.Decrypt(Arg.Any<string>())
            .Returns(x => x[0] is "CIPHER_ENDPOINT"
                ? "https://fake.openai.azure.com/"
                : "fake-api-key-00000000000000000000000000000000");

        // Act — modelId is passed as a parameter, not stored on the entity.
        await _sut.ResolveAsync(workspaceId, "gpt-4o", CancellationToken.None);

        // Assert — single query, no existence-check pre-query.
        await _repository.Received(1).GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>());
    }
}
