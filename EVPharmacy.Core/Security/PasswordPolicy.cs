
namespace EVPharmacy.Core.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 8;

    /// <summary>
    /// Returns a user-friendly error message, or null when the password is acceptable.
    /// </summary>
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        if (password.Length < MinimumLength)
        {
            return $"Password must be at least {MinimumLength} characters long.";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "Password must contain at least one letter and one number.";
        }

        return null;
    }
}
