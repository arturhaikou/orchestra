using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Orchestra.Domain.Interfaces;

namespace Orchestra.Infrastructure.Security;

/// <summary>
/// Implementation of credential encryption using AES-256-GCM authenticated encryption.
/// </summary>
public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly byte[] _encryptionKey;
    private const int NonceSize = 12; // 96 bits for AES-GCM (recommended size)
    private const int TagSize = 16;   // 128 bits for authentication tag

    public CredentialEncryptionService(IConfiguration configuration)
    {
        var keyString = configuration["Encryption:Key"] 
            ?? throw new InvalidOperationException(
                "Encryption key not configured. Set 'Encryption:Key' in configuration.");
        
        try
        {
            _encryptionKey = Convert.FromBase64String(keyString);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encryption key must be a valid Base64 string.", ex);
        }
        
        if (_encryptionKey.Length != 32)
        {
            throw new InvalidOperationException(
                $"Encryption key must be 256 bits (32 bytes). Current length: {_encryptionKey.Length} bytes. " +
                "Generate a valid key using: openssl rand -base64 32");
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            throw new ArgumentException("Plain text cannot be null or empty.", nameof(plainText));
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[NonceSize];
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        // Generate cryptographically random nonce
        RandomNumberGenerator.Fill(nonce);

        // Encrypt with AES-GCM
        using var aesGcm = new AesGcm(_encryptionKey, TagSize);
        aesGcm.Encrypt(nonce, plainBytes, ciphertext, tag);

        // Combine: nonce + ciphertext + tag
        var combined = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length + ciphertext.Length, tag.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
        {
            throw new ArgumentException("Encrypted text cannot be null or empty.", nameof(encryptedText));
        }

        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(encryptedText);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Encrypted text must be a valid Base64 string.", nameof(encryptedText), ex);
        }

        if (combined.Length < NonceSize + TagSize)
        {
            throw new ArgumentException(
                $"Invalid encrypted data format. Minimum length: {NonceSize + TagSize} bytes, " +
                $"actual: {combined.Length} bytes.", 
                nameof(encryptedText));
        }

        // Extract components
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[combined.Length - NonceSize - TagSize];

        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(combined, NonceSize, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(combined, NonceSize + ciphertext.Length, tag, 0, TagSize);

        var plainBytes = new byte[ciphertext.Length];

        // Decrypt and validate authentication tag
        using var aesGcm = new AesGcm(_encryptionKey, TagSize);
        try
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException(
                "Failed to decrypt: authentication tag validation failed. " +
                "Data may have been tampered with or key is incorrect.", ex);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }
}