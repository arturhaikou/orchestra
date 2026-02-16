namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Service for hashing and verifying passwords using PBKDF2.
/// </summary>
public interface IPasswordHashingService
{
    /// <summary>
    /// Hashes a password using PBKDF2 with a randomly generated salt.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>Base64-encoded string in format "{salt}:{hash}".</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a stored hash.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="passwordHash">The stored password hash in format "{salt}:{hash}".</param>
    /// <returns>True if password matches; false otherwise.</returns>
    bool VerifyPassword(string password, string passwordHash);
}