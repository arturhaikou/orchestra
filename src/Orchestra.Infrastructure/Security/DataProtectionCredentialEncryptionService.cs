using Microsoft.AspNetCore.DataProtection;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Security;

/// <summary>
/// ASP.NET Data Protection-backed implementation of <see cref="IProviderCredentialEncryptionService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Encrypts and decrypts AI provider credential strings (<c>Endpoint</c>, <c>ApiKey</c>) using the
/// platform's built-in symmetric key ring, scoped to a fixed, internal purpose string
/// (<c>"Orchestra.AIProviderCredentials"</c>). The purpose string must never be changed after
/// first deployment — modifying it renders all existing ciphertext permanently unreadable.
/// </para>
/// <para>
/// <b>Security invariants:</b> Neither method logs the plaintext or ciphertext it processes.
/// If <see cref="Decrypt"/> receives tampered, expired, or foreign-key ciphertext, the underlying
/// <see cref="System.Security.Cryptography.CryptographicException"/> propagates to the caller
/// unmodified — it is never swallowed or wrapped.
/// </para>
/// </remarks>
public sealed class DataProtectionCredentialEncryptionService : IProviderCredentialEncryptionService
{
    private const string Purpose = "Orchestra.AIProviderCredentials";

    private readonly IDataProtector _protector;

    /// <summary>
    /// Initialises a new instance and derives a scoped <see cref="IDataProtector"/> for the
    /// <c>"Orchestra.AIProviderCredentials"</c> purpose sub-key.
    /// </summary>
    /// <param name="provider">The platform data-protection provider supplied by DI.</param>
    public DataProtectionCredentialEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    /// <inheritdoc/>
    public string Encrypt(string plainText)
        => _protector.Protect(plainText);

    /// <inheritdoc/>
    public string Decrypt(string cipherText)
        => _protector.Unprotect(cipherText);
}
