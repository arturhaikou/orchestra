using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Security;

/// <summary>
/// Service for hashing and verifying passwords using PBKDF2.
/// </summary>
public class PasswordHashingService : IPasswordHashingService
{
    private const int IterationCount = 100000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        // Generate a 128-bit salt using a cryptographically strong random sequence of nonzero values
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        // Derive a 256-bit subkey (use HMACSHA256 with 100,000 iterations)
        byte[] hash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: IterationCount,
            numBytesRequested: KeySize);

        // Combine salt and hash
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string passwordHash)
    {
        // Split the stored hash into salt and hash parts
        var parts = passwordHash.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        // Extract the salt from the stored hash
        byte[] salt;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
        }
        catch (FormatException)
        {
            return false;
        }

        // Extract the stored hash
        byte[] storedHash;
        try
        {
            storedHash = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        // Compute the hash for the provided password using the same salt
        byte[] computedHash = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: IterationCount,
            numBytesRequested: KeySize);

        // Compare the computed hash with the stored hash
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }
}