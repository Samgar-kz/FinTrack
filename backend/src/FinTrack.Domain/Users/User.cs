using FinTrack.Domain.Common;

namespace FinTrack.Domain.Users;

public enum UserRole
{
    User = 0,
    Admin = 1,
}

public sealed class User : Entity, IAuditable
{
    private User()
    {
        // Required by EF Core.
    }

    public User(string email, string fullName, string baseCurrency, UserRole role = UserRole.User)
    {
        Email = NormalizeEmail(email);
        SetFullName(fullName);
        BaseCurrency = Money.NormalizeCurrency(baseCurrency);
        Role = role;
    }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    /// <summary>Currency used by default on the dashboard and in reports.</summary>
    public string BaseCurrency { get; private set; } = "CAD";

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = passwordHash;
    }

    public void UpdateProfile(string fullName, string baseCurrency)
    {
        SetFullName(fullName);
        BaseCurrency = Money.NormalizeCurrency(baseCurrency);
    }

    public void RecordLogin(DateTimeOffset at) => LastLoginAt = at;

    private void SetFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("Full name is required.");
        }

        FullName = fullName.Trim();
    }
}
