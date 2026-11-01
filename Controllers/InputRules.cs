using System.Net.Mail;

/// <summary>Shared validation for user and image input.</summary>
public static class InputRules
{
    public const int MaxEmailLength = 100;   // matches the users.email column
    public const int MaxNameLength = 100;    // matches the users.name column
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 72; // BCrypt ignores bytes beyond 72
    public const int MaxUrlLength = 2048;
    public const int MaxTextLength = 300;

    // Guest accounts use this domain; nobody may register with it
    public const string GuestEmailDomain = "guest.machinemal.local";

    /// <summary>Trimmed, lower-cased email, or null if missing/invalid.</summary>
    public static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxEmailLength) return null;
        try
        {
            var parsed = new MailAddress(trimmed);
            return parsed.Address == trimmed && trimmed.Contains('.', StringComparison.Ordinal) ? trimmed : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static bool IsGuestEmail(string email) =>
        email.EndsWith("@" + GuestEmailDomain, StringComparison.OrdinalIgnoreCase);

    public static string? PasswordProblem(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters.";
        if (System.Text.Encoding.UTF8.GetByteCount(password) > MaxPasswordLength)
            return $"Password must be at most {MaxPasswordLength} bytes.";
        return null;
    }

    public static string? NameProblem(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return "Name is required.";
        if (trimmed.Length > MaxNameLength) return $"Name must be at most {MaxNameLength} characters.";
        return null;
    }

    /// <summary>True for absolute http(s) URLs within the length limit.</summary>
    public static bool IsHttpUrl(string? url) =>
        !string.IsNullOrEmpty(url)
        && url.Length <= MaxUrlLength
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
