using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;

namespace FinTrack.UnitTests.Domain;

internal static class TestData
{
    public static readonly Guid UserId = Guid.CreateVersion7();
    public static readonly DateOnly Today = new(2026, 11, 15);

    public static Account Account(decimal opening = 1_000m, string currency = "CAD", Guid? userId = null) =>
        new(userId ?? UserId, $"Account {Guid.NewGuid():N}"[..20], AccountType.Checking, currency, opening);

    public static Category Expense(Guid? userId = null) => new(userId ?? UserId, "Groceries", CategoryKind.Expense, "#22C55E");

    public static Category Income(Guid? userId = null) => new(userId ?? UserId, "Salary", CategoryKind.Income, "#16A34A");
}
