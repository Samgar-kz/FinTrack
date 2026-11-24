using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;

namespace FinTrack.Domain.Budgets;

/// <summary>A monthly spending limit for one expense category, in a single currency.</summary>
public sealed class Budget : Entity, IAuditable
{
    private Budget()
    {
        // Required by EF Core.
    }

    public Budget(Category category, int year, int month, string currency, decimal limit)
    {
        if (category.Kind != CategoryKind.Expense)
        {
            throw new DomainException("Budgets can only be set for expense categories.");
        }

        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw new DomainException("Budget period is invalid.");
        }

        UserId = category.UserId;
        CategoryId = category.Id;
        Year = year;
        Month = month;
        Currency = Money.NormalizeCurrency(currency);
        ChangeLimit(limit);
    }

    public Guid UserId { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category Category { get; private set; } = null!;

    public int Year { get; private set; }

    public int Month { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal Limit { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public DateOnly PeriodStart => new(Year, Month, 1);

    public DateOnly PeriodEnd => PeriodStart.AddMonths(1).AddDays(-1);

    public void ChangeLimit(decimal limit)
    {
        if (!Money.IsValidPositiveAmount(limit))
        {
            throw new DomainException("Budget limit must be positive and have at most two decimal places.");
        }

        Limit = limit;
    }
}
