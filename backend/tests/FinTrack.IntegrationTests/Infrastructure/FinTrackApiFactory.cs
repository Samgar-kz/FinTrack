using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinTrack.Api.Controllers;
using FinTrack.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FinTrack.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container, so tests exercise the same
/// migrations, SQL translation and constraints as production.
/// </summary>
public sealed class FinTrackApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@fintrack.test";
    public const string AdminPassword = "Admin123!";
    public const string DemoEmail = "demo@fintrack.test";
    public const string DemoPassword = "Demo123!";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Optional connection string to an existing PostgreSQL server (used when Docker is unavailable).
    /// A uniquely named database is created on it for the test run and dropped afterwards.
    /// </summary>
    public const string ExternalDatabaseVariable = "FINTRACK_TEST_DATABASE";

    private readonly string? _externalServer = Environment.GetEnvironmentVariable(ExternalDatabaseVariable);
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_externalServer))
        {
            _connectionString = new NpgsqlConnectionStringBuilder(_externalServer)
            {
                Database = $"fintrack_tests_{Guid.NewGuid():N}",
            }.ConnectionString;
            return;
        }

        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();

        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(_externalServer) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>Registers a brand-new user and returns a client authenticated as that user.</summary>
    public async Task<(HttpClient Client, UserDto User)> CreateUserClientAsync(string currency = "CAD")
    {
        var client = CreateClient();
        var email = $"user-{Guid.NewGuid():N}@fintrack.test";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Sup3rSecret!", "Test User", currency),
            Json);
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User);
    }

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Seed:DemoEmail", DemoEmail);
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<FinTrackApiFactory>
{
    public const string Name = "api";
}

internal static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, FinTrackApiFactory.Json)!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, FinTrackApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, FinTrackApiFactory.Json);
}
