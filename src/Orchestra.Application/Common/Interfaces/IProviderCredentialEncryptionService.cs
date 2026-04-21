namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Encrypts and decrypts AI provider credential strings (endpoints, API keys).
/// </summary>
/// <remarks>
/// <para>
/// <b>Security contract:</b> Both methods accept and return <see langword="string"/> only.
/// Raw byte arrays must never appear in the public surface; callers must not attempt to
/// interpret the ciphertext returned by <see cref="Encrypt"/> as anything other than an
/// opaque string to be stored and later passed back to <see cref="Decrypt"/>.
/// </para>
/// <para>
/// The synchronous signature is deliberate — symmetric encryption is a CPU-bound operation
/// with no I/O; wrapping it in async would add unnecessary overhead.
/// </para>
/// </remarks>
public interface IProviderCredentialEncryptionService
{
    /// <summary>
    /// Encrypts <paramref name="plainText"/> and returns the resulting ciphertext.
    /// </summary>
    /// <param name="plainText">The plaintext credential value to encrypt.</param>
    /// <returns>An opaque ciphertext string suitable for storage.</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts <paramref name="cipherText"/> and returns the original plaintext value.
    /// </summary>
    /// <param name="cipherText">The ciphertext string previously produced by <see cref="Encrypt"/>.</param>
    /// <returns>The original plaintext credential value.</returns>
    string Decrypt(string cipherText);
}
