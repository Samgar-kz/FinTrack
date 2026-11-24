using FinTrack.Domain.Accounts;
using FinTrack.Domain.Budgets;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;
using static FinTrack.UnitTests.Domain.TestData;

namespace FinTrack.UnitTests.Domain;

public sealed class AccountTests
{
    [Fact]
    public void New_account_starts_at_its_opening_balance()
    {
        var account = new Account(UserId, "  Visa  ", AccountType.CreditCard, "cad", -250.75m);

        Assert.Equal(-250.75m, account.Balance);
        Assert.Equal("Visa", account.Name);
        Assert.Equal("CAD", account.Currency);
    }

    [Fact]
    public void Unsupported_currency_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Account(UserId, "Wallet", AccountType.Cash, "XYZ", 0m));
    }

    [Fact]
    public void Opening_balance_with_more_than_two_decimals_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Account(UserId, "Wallet", AccountType.Cash, "CAD", 1.001m));
    }

    [Fact]
    public void Restored_account_accepts_postings_again()
    {
        var account = Account(opening: 0m);
        account.Archive();
        account.Restore();

        Transaction.Record(account, TransactionType.Income, 10m, Today, "Refund", null);

        Assert.Equal(10m, account.Balance);
    }
}

public sealed class BudgetTests
{
    [Fact]
    public void Budget_period_covers_the_whole_month()
    {
        var budget = new Budget(Expense(), 2028, 2, "CAD", 300m);

        Assert.Equal(new DateOnly(2028, 2, 1), budget.PeriodStart);
        Assert.Equal(new DateOnly(2028, 2, 29), budget.PeriodEnd);
    }

    [Fact]
    public void Budgets_require_an_expense_category()
    {
        Assert.Throws<DomainException>(() => new Budget(Income(), 2026, 11, "CAD", 100m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.234)]
    public void Budget_limit_must_be_a_positive_amount(decimal limit)
    {
        Assert.Throws<DomainException>(() => new Budget(Expense(), 2026, 11, "CAD", limit));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 5)]
    public void Budget_period_must_be_valid(int year, int month)
    {
        Assert.Throws<DomainException>(() => new Budget(Expense(), year, month, "CAD", 100m));
    }
}

public sealed class CategoryTests
{
    [Theory]
    [InlineData("#22C55E", true)]
    [InlineData("#abcdef", true)]
    [InlineData("22C55E", false)]
    [InlineData("#22C55", false)]
    [InlineData("red", false)]
    public void Colour_must_be_a_six_digit_hex_value(string color, bool expected)
    {
        Assert.Equal(expected, Category.IsValidColor(color));
    }

    [Fact]
    public void Expense_categories_only_accept_expenses()
    {
        var category = Expense();

        Assert.True(category.Accepts(TransactionType.Expense));
        Assert.False(category.Accepts(TransactionType.Income));
        Assert.False(category.Accepts(TransactionType.TransferIn));
    }

    [Fact]
    public void Default_categories_cover_income_and_expenses_with_unique_names()
    {
        var defaults = Category.CreateDefaults(UserId);

        Assert.Contains(defaults, c => c.Kind == CategoryKind.Income);
        Assert.Contains(defaults, c => c.Kind == CategoryKind.Expense);
        Assert.Equal(defaults.Count, defaults.Select(c => (c.Kind, c.Name)).Distinct().Count());
        Assert.All(defaults, c => Assert.Equal(UserId, c.UserId));
    }
}
