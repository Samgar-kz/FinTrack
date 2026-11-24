using FinTrack.Application.Accounts;
using FinTrack.Application.Admin;
using FinTrack.Application.Auth;
using FinTrack.Application.Budgets;
using FinTrack.Application.Categories;
using FinTrack.Application.Reports;
using FinTrack.Application.Transactions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<AuthService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<ReportService>();
        services.AddScoped<AdminService>();

        return services;
    }
}
