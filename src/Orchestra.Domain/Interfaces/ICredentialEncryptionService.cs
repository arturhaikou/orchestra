namespace Orchestra.Domain.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive credentials using AES-256-GCM.
/// </summary>
public interface ICredentialEncryptionService
{
    /// <summary>
    /// Encrypts a plain text credential using AES-256-GCM authenticated encryption.
    /// </summary>
    /// <param name="plainText">The plain text credential to encrypt.</param>
    /// <returns>The encrypted credential as a Base64-encoded string.</returns>
    /// <exception cref="ArgumentException">Thrown when plainText is null or empty.</exception>
    string Encrypt(string plainText);
    
    /// <summary>
    /// Decrypts an encrypted credential using AES-256-GCM authenticated encryption.
    /// </summary>
    /// <param name="encryptedText">The Base64-encoded encrypted credential to decrypt.</param>
    /// <returns>The decrypted plain text credential.</returns>
    /// <exception cref="ArgumentException">Thrown when encryptedText is null or empty.</exception>
    /// <exception cref="CryptographicException">Thrown when authentication tag validation fails.</exception>
    string Decrypt(string encryptedText);
}