using FinTrack.Domain.Accounts;
using FinTrack.Domain.Budgets;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Abstractions;

/// <summary>Unit of work exposed to the application layer. Implemented by the EF Core context.</summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Account> Accounts { get; }

    DbSet<Category> Categories { get; }

    DbSet<Transaction> Transactions { get; }

    DbSet<Budget> Budgets { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>The authenticated caller of the current request.</summary>
public interface ICurrentUser
{
    /// <summary>Id of the authenticated user. Throws when the request is anonymous.</summary>
    Guid Id { get; }

    bool IsAdmin { get; }
}

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordCheck Verify(User user, string password);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    IssuedRefreshToken CreateRefreshToken();

    string HashRefreshToken(string refreshToken);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>A newly generated refresh token. <see cref="Token"/> goes to the client; only <see cref="Hash"/> is stored.</summary>
public sealed record IssuedRefreshToken(string Token, string Hash, DateTimeOffset ExpiresAt);
