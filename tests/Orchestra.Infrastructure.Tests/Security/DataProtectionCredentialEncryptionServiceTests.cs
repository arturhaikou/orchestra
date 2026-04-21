using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.Security;

namespace Orchestra.Infrastructure.Tests.Security;

/// <summary>
/// Unit tests for <see cref="DataProtectionCredentialEncryptionService"/> covering
/// BDD Scenarios 1–4 from FR-02.
/// </summary>
public class DataProtectionCredentialEncryptionServiceTests
{
    private readonly DataProtectionCredentialEncryptionService _sut;

    public DataProtectionCredentialEncryptionServiceTests()
    {
        // EphemeralDataProtectionProvider stores keys in-memory only — safe for unit tests,
        // requires no file system or configuration, and is destroyed after the test run.
        var provider = new EphemeralDataProtectionProvider();
        _sut = new DataProtectionCredentialEncryptionService(provider);
    }

    // ── Scenario 1: Encrypt produces a non-empty ciphertext different from input ───────

    [Fact]
    public void Encrypt_WithValidPlainText_ReturnsNonEmptyStringDifferentFromInput()
    {
        // Arrange
        const string plainText = "my-secret-api-key";

        // Act
        var cipherText = _sut.Encrypt(plainText);

        // Assert
        Assert.NotNull(cipherText);
        Assert.NotEmpty(cipherText);
        Assert.NotEqual(plainText, cipherText);
    }

    // ── Scenario 2: Decrypt recovers the original plaintext ─────────────────────────

    [Fact]
    public void Decrypt_WithCipherTextProducedByEncrypt_ReturnsOriginalPlainText()
    {
        // Arrange
        const string plainText = "my-secret-api-key";
        var cipherText = _sut.Encrypt(plainText);

        // Act
        var recovered = _sut.Decrypt(cipherText);

        // Assert
        Assert.Equal(plainText, recovered);
    }

    // ── Scenario 3: Decrypt throws CryptographicException on tampered ciphertext ─────

    [Fact]
    public void Decrypt_WithTamperedCipherText_ThrowsCryptographicException()
    {
        // Arrange
        const string tamperedCipherText = "this-is-not-valid-ciphertext";

        // Act & Assert
        Assert.Throws<CryptographicException>(() => _sut.Decrypt(tamperedCipherText));
    }

    // ── Scenario 4: DI container resolves DataProtectionCredentialEncryptionService ──

    [Fact]
    public void DI_WhenContainerBuilt_ResolvesDataProtectionCredentialEncryptionService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDataProtection();
        services.AddScoped<IProviderCredentialEncryptionService, DataProtectionCredentialEncryptionService>();
        var provider = services.BuildServiceProvider();

        // Act
        var resolved = provider.GetRequiredService<IProviderCredentialEncryptionService>();

        // Assert
        Assert.IsType<DataProtectionCredentialEncryptionService>(resolved);
    }
}
