using FinTrack.Application.Abstractions;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Budgets;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinTrack.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Creates the demo and admin users with twelve months of sample data. Never enable in production.</summary>
    public bool Enabled { get; set; }

    public string DemoEmail { get; set; } = "demo@fintrack.dev";

    public string DemoPassword { get; set; } = string.Empty;

    public string AdminEmail { get; set; } = "admin@fintrack.dev";

    public string AdminPassword { get; set; } = string.Empty;
}

/// <summary>Creates realistic, deterministic demo data so the app is explorable right after start-up.</summary>
public sealed class DataSeeder(
    AppDbContext db,
    IPasswordHasher passwordHasher,
    TimeProvider clock,
    IOptions<SeedOptions> options,
    ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.DemoPassword) || string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            throw new InvalidOperationException("Seed:DemoPassword and Seed:AdminPassword must be set when seeding is enabled.");
        }

        if (!await db.Users.AnyAsync(u => u.Email == User.NormalizeEmail(settings.AdminEmail), ct))
        {
            var admin = new User(settings.AdminEmail, "Platform Admin", "CAD", UserRole.Admin);
            admin.SetPasswordHash(passwordHasher.Hash(admin, settings.AdminPassword));
            db.Users.Add(admin);
            db.Categories.AddRange(Category.CreateDefaults(admin.Id));
            logger.LogInformation("Seeded admin user {Email}", admin.Email);
        }

        if (!await db.Users.AnyAsync(u => u.Email == User.NormalizeEmail(settings.DemoEmail), ct))
        {
            SeedDemoUser(settings);
            logger.LogInformation("Seeded demo user {Email} with sample data", settings.DemoEmail);
        }

        await db.SaveChangesAsync(ct);
    }

    private void SeedDemoUser(SeedOptions settings)
    {
        var user = new User(settings.DemoEmail, "Alex Morgan", "CAD");
        user.SetPasswordHash(passwordHasher.Hash(user, settings.DemoPassword));
        db.Users.Add(user);

        var categories = Category.CreateDefaults(user.Id).ToDictionary(c => c.Name);
        db.Categories.AddRange(categories.Values);

        var chequing = new Account(user.Id, "Everyday Chequing", AccountType.Checking, "CAD", 3_200m);
        var savings = new Account(user.Id, "High-Interest Savings", AccountType.Savings, "CAD", 12_500m);
        var card = new Account(user.Id, "Visa Infinite", AccountType.CreditCard, "CAD", 0m);
        var travel = new Account(user.Id, "USD Travel Account", AccountType.Checking, "USD", 1_450m);
        db.Accounts.AddRange(chequing, savings, card, travel);

        var random = new Random(20_24);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);

        void Add(Account account, TransactionType type, decimal amount, DateOnly date, string description, string category)
        {
            if (date > today)
            {
                return;
            }

            db.Transactions.Add(Transaction.Record(account, type, amount, date, description, categories[category]));
        }

        decimal Between(int min, int max) => decimal.Round(min + (decimal)random.NextDouble() * (max - min), 2);

        for (var month = firstMonth; month <= today; month = month.AddMonths(1))
        {
            var days = DateTime.DaysInMonth(month.Year, month.Month);
            DateOnly Day(int day) => month.AddDays(Math.Min(day, days) - 1);

            Add(chequing, TransactionType.Income, 3_150m, Day(1), "Payroll - Northwind Systems", "Salary");
            Add(chequing, TransactionType.Income, 3_150m, Day(15), "Payroll - Northwind Systems", "Salary");
            Add(chequing, TransactionType.Expense, 2_150m, Day(1), "Rent - Maple Residences", "Housing");
            Add(chequing, TransactionType.Expense, Between(95, 140), Day(6), "Hydro & Internet", "Utilities");
            Add(chequing, TransactionType.Expense, 156m, Day(2), "Monthly transit pass", "Transportation");
            Add(card, TransactionType.Expense, 17.99m, Day(9), "Streaming subscription", "Subscriptions");
            Add(card, TransactionType.Expense, 12.99m, Day(12), "Music subscription", "Subscriptions");

            for (var week = 0; week < 4; week++)
            {
                Add(card, TransactionType.Expense, Between(85, 165), Day(3 + (week * 7)), "Grocery store", "Groceries");
            }

            for (var i = 0; i < 3; i++)
            {
                Add(card, TransactionType.Expense, Between(25, 95), Day(5 + (i * 9)), "Restaurant", "Dining Out");
            }

            Add(card, TransactionType.Expense, Between(40, 180), Day(18), "Online shopping", "Shopping");
            Add(card, TransactionType.Expense, Between(30, 90), Day(21), "Cinema & events", "Entertainment");

            if (random.Next(3) == 0)
            {
                Add(chequing, TransactionType.Income, Between(400, 1_200), Day(24), "Freelance project", "Freelance");
            }

            if (month.Month % 3 == 0)
            {
                Add(savings, TransactionType.Income, Between(60, 90), Day(28), "Interest payment", "Investments");
                Add(travel, TransactionType.Expense, Between(120, 380), Day(20), "Hotel booking", "Travel");
            }

            var transferDate = Day(16);
            if (transferDate <= today)
            {
                var (toSavingsOut, toSavingsIn) = Transaction.CreateTransfer(chequing, savings, 750m, transferDate, "Monthly savings");
                db.Transactions.AddRange(toSavingsOut, toSavingsIn);
            }

            var paymentDate = Day(25);
            if (paymentDate <= today && month < new DateOnly(today.Year, today.Month, 1))
            {
                var (payOut, payIn) = Transaction.CreateTransfer(chequing, card, 900m, paymentDate, "Credit card payment");
                db.Transactions.AddRange(payOut, payIn);
            }
        }

        var budgets = new (string Category, decimal Limit)[]
        {
            ("Groceries", 550m), ("Dining Out", 220m), ("Transportation", 180m), ("Utilities", 160m),
            ("Shopping", 200m), ("Entertainment", 100m), ("Subscriptions", 40m),
        };

        foreach (var (category, limit) in budgets)
        {
            db.Budgets.Add(new Budget(categories[category], today.Year, today.Month, "CAD", limit));
        }
    }
}
