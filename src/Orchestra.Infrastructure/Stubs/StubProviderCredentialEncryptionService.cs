using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Stubs;

/// <summary>
/// Phase 1 stub. Throws <see cref="NotImplementedException"/> for every method.
/// Replace with a real AES-256-GCM implementation in Phase 2.
/// </summary>
public sealed class StubProviderCredentialEncryptionService : IProviderCredentialEncryptionService
{
    /// <inheritdoc/>
    public string Encrypt(string plainText)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public string Decrypt(string cipherText)
        => throw new NotImplementedException();
}
