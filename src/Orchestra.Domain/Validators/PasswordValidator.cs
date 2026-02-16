namespace Orchestra.Domain.Validators;

/// <summary>
/// Validates password strength requirements.
/// </summary>
public static class PasswordValidator
{
    private const int MinLength = 8;
    private const int MaxLength = 128;

    /// <summary>
    /// Validates that a password meets all strength requirements.
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <returns>Tuple indicating if valid and error message if not.</returns>
    public static (bool IsValid, string? Error) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Password is required");
        }

        if (password.Length < MinLength)
        {
            return (false, $"Password must be at least {MinLength} characters");
        }

        if (password.Length > MaxLength)
        {
            return (false, $"Password must not exceed {MaxLength} characters");
        }

        if (!password.Any(char.IsUpper))
        {
            return (false, "Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            return (false, "Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            return (false, "Password must contain at least one digit");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            return (false, "Password must contain at least one special character");
        }

        return (true, null);
    }
}