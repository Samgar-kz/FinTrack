using FinTrack.Application.Abstractions;
using FinTrack.Application.Common;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Auth;

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider clock,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<UpdateProfileRequest> profileValidator)
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "The session has expired. Please sign in again.";

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var email = User.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User(email, request.FullName, request.BaseCurrency);
        user.SetPasswordHash(passwordHasher.Hash(user, request.Password));
        user.RecordLogin(clock.GetUtcNow());

        db.Users.Add(user);
        db.Categories.AddRange(Category.CreateDefaults(user.Id));

        var result = IssueTokens(user);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            // Hash anyway so response time does not reveal whether the email is registered.
            passwordHasher.Hash(new User("timing@fintrack.local", "Timing", "CAD"), request.Password);
            throw new UnauthorizedException(InvalidCredentials);
        }

        var check = passwordHasher.Verify(user, request.Password);
        if (check == PasswordCheck.Failed)
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.Hash(user, request.Password));
        }

        user.RecordLogin(clock.GetUtcNow());
        var result = IssueTokens(user);
        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// Exchanges a refresh token for a new token pair. Tokens are single-use; presenting a
    /// token that was already rotated is treated as theft and revokes every session of the user.
    /// </summary>
    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        var hash = tokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        var now = clock.GetUtcNow();

        if (stored is null)
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (!stored.IsActive(now))
        {
            if (stored.RevokedAt is not null)
            {
                await RevokeAllSessionsAsync(stored.UserId, now, ct);
            }

            throw new UnauthorizedException(InvalidRefreshToken);
        }

        var user = await db.Users.SingleAsync(u => u.Id == stored.UserId, ct);
        var result = IssueTokens(user, stored);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = tokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
        {
            return;
        }

        stored.Revoke(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User", userId);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        await profileValidator.ValidateAndThrowAsync(request, ct);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User", userId);
        user.UpdateProfile(request.FullName, request.BaseCurrency);
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    private AuthResult IssueTokens(User user, RefreshToken? replacing = null)
    {
        var now = clock.GetUtcNow();
        var accessToken = tokenService.CreateAccessToken(user);
        var issued = tokenService.CreateRefreshToken();

        var refreshToken = new RefreshToken(user.Id, issued.Hash, now, issued.ExpiresAt);
        db.RefreshTokens.Add(refreshToken);
        replacing?.Revoke(now, refreshToken.Id);

        return new AuthResult(accessToken.Token, accessToken.ExpiresAt, issued.Token, issued.ExpiresAt, user.ToDto());
    }

    private async Task RevokeAllSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in active)
        {
            token.Revoke(now);
        }

        await db.SaveChangesAsync(ct);
    }
}
