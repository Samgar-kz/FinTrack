using FinTrack.Application.Abstractions;
using FinTrack.Infrastructure.Persistence;
using FinTrack.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinTrack.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Database";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AuditableEntityInterceptor>();

        // The connection string is resolved lazily so test hosts can override configuration.
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(provider.GetRequiredService<AuditableEntityInterceptor>());
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<DataSeeder>();

        return services;
    }
}
