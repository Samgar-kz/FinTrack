using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinTrack.Api.Controllers;
using FinTrack.Application.Auth;
using FinTrack.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(FinTrackApiFactory factory)
{
    private HttpClient RawClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Register_returns_an_access_token_and_an_http_only_refresh_cookie()
    {
        var client = RawClient();

        var response = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"new-{Guid.NewGuid():N}@fintrack.test", "Sup3rSecret!", "New User", "CAD"));
        var auth = await response.ReadAsync<AuthResponse>();

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith(AuthController.RefreshCookieName + "=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_returns_conflict()
    {
        var email = $"dup-{Guid.NewGuid():N}@fintrack.test";
        var client = RawClient();
        (await client.PostJsonAsync("/api/auth/register", new RegisterRequest(email, "Sup3rSecret!", "A", "CAD"))).EnsureSuccessStatusCode();

        var second = await client.PostJsonAsync("/api/auth/register", new RegisterRequest(email.ToUpperInvariant(), "Sup3rSecret!", "B", "CAD"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Invalid_registration_returns_validation_problem_details()
    {
        var response = await RawClient().PostJsonAsync("/api/auth/register", new RegisterRequest("not-an-email", "weak", "", "XYZ"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("baseCurrency", out _));
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_unauthorized()
    {
        var response = await RawClient().PostJsonAsync("/api/auth/login", new LoginRequest(FinTrackApiFactory.DemoEmail, "Wrong123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_token()
    {
        var response = await RawClient().GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reuse_of_an_old_token_revokes_the_session()
    {
        var client = RawClient();
        var register = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"rotate-{Guid.NewGuid():N}@fintrack.test", "Sup3rSecret!", "Rotator", "CAD"));
        register.EnsureSuccessStatusCode();
        var first = CookieValue(register);

        var refreshed = await Refresh(client, first);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = CookieValue(refreshed);
        Assert.NotEqual(first, second);

        // Replaying the already-rotated token is treated as theft...
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, first)).StatusCode);

        // ...which revokes the whole session, including the newest token.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, second)).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var client = RawClient();
        var login = await client.PostJsonAsync("/api/auth/login", new LoginRequest(FinTrackApiFactory.DemoEmail, FinTrackApiFactory.DemoPassword));
        var token = CookieValue(login);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"{AuthController.RefreshCookieName}={token}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(client, token)).StatusCode);
    }

    [Fact]
    public async Task Profile_can_be_read_and_updated()
    {
        var (client, user) = await factory.CreateUserClientAsync();

        var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<UserDto>();
        Assert.Equal(user.Email, me.Email);

        var updated = await (await client.PutJsonAsync("/api/auth/me", new UpdateProfileRequest("Renamed User", "USD"))).ReadAsync<UserDto>();
        Assert.Equal("Renamed User", updated.FullName);
        Assert.Equal("USD", updated.BaseCurrency);
    }

    private static async Task<HttpResponseMessage> Refresh(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{AuthController.RefreshCookieName}={token}");
        return await client.SendAsync(request);
    }

    private static string CookieValue(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AuthController.RefreshCookieName, StringComparison.Ordinal));
        return header.Split(';')[0][(AuthController.RefreshCookieName.Length + 1)..];
    }
}
