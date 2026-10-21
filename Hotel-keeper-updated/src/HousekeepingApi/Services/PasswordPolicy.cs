namespace HousekeepingApi.Services;

public static class PasswordPolicy
{
    public const int MinLength = 10;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return $"Password must be at least {MinLength} characters.";
        if (password.Length > 128) return "Password must be at most 128 characters.";
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            return "Password must contain an uppercase letter, a lowercase letter and a digit.";
        return null;
    }
}
