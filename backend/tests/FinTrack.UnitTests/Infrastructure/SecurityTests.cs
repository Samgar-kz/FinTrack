using FinTrack.Application.Abstractions;
using FinTrack.Domain.Users;
using FinTrack.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinTrack.UnitTests.Infrastructure;

public sealed class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 30, 0, TimeSpan.Zero);

    private readonly JwtOptions _options = new()
    {
        Issuer = "fintrack-tests",
        Audience = "fintrack-tests-web",
        SigningKey = "unit-test-signing-key-0123456789abcdef",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 14,
    };

    private JwtTokenService CreateService() => new(Options.Create(_options), new FakeTimeProvider(Now));

    [Fact]
    public void Access_token_carries_identity_claims_and_expiry()
    {
        var user = new User("alex@example.com", "Alex Morgan", "CAD", UserRole.Admin);

        var token = CreateService().CreateAccessToken(user);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Token);

        Assert.Equal(Now.AddMinutes(15), token.ExpiresAt);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("fintrack-tests", jwt.Issuer);
        Assert.Contains("fintrack-tests-web", jwt.Audiences);
        Assert.Equal("Admin", jwt.GetClaim(FinTrackClaims.Role).Value);
        Assert.Equal("alex@example.com", jwt.GetClaim(FinTrackClaims.Email).Value);
    }

    [Fact]
    public void Refresh_tokens_are_random_and_only_their_hash_is_stored()
    {
        var service = CreateService();

        var first = service.CreateRefreshToken();
        var second = service.CreateRefreshToken();

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Token, first.Hash);
        Assert.Equal(64, first.Hash.Length);
        Assert.Equal(first.Hash, service.HashRefreshToken(first.Token));
        Assert.Equal(Now.AddDays(14), first.ExpiresAt);
    }
}

public sealed class PasswordHasherTests
{
    [Fact]
    public void Verifies_correct_password_and_rejects_wrong_one()
    {
        var hasher = new IdentityPasswordHasher();
        var user = new User("alex@example.com", "Alex", "CAD");
        user.SetPasswordHash(hasher.Hash(user, "Correct1Horse"));

        Assert.Equal(PasswordCheck.Success, hasher.Verify(user, "Correct1Horse"));
        Assert.Equal(PasswordCheck.Failed, hasher.Verify(user, "correct1horse"));
        Assert.DoesNotContain("Correct1Horse", user.PasswordHash, StringComparison.Ordinal);
    }
}
