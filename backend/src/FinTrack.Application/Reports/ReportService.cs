using FinTrack.Application.Abstractions;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Reports;

public sealed record SummaryDto(
    string Currency,
    DateOnly From,
    DateOnly To,
    decimal Income,
    decimal Expenses,
    decimal Net,
    decimal SavingsRate,
    decimal TotalBalance,
    int TransactionCount);

public sealed record CategoryBreakdownDto(
    Guid? CategoryId,
    string Name,
    string Color,
    decimal Amount,
    decimal Percentage,
    int TransactionCount);

public sealed record CashFlowPointDto(int Year, int Month, decimal Income, decimal Expenses, decimal Net);

/// <summary>
/// Aggregated reporting. All figures are for a single currency: amounts in different
/// currencies are never summed together, because FinTrack does not invent exchange rates.
/// Transfers between the user's own accounts are excluded from income and expenses.
/// </summary>
public sealed class ReportService(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private const int MaxRangeDays = 3_660;
    private const string UncategorisedColor = "#94A3B8";

    public async Task<SummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, string? currency, CancellationToken ct)
    {
        var (start, end) = ResolvePeriod(from, to);
        var code = await ResolveCurrencyAsync(currency, ct);

        var totals = await IncomeAndExpenses(start, end, code)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var income = totals.Where(t => t.Type == TransactionType.Income).Sum(t => t.Total);
        var expenses = totals.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Total);

        var balance = await db.Accounts.AsNoTracking()
            .Where(a => a.UserId == currentUser.Id && !a.IsArchived && a.Currency == code)
            .SumAsync(a => a.Balance, ct);

        var savingsRate = income > 0 ? decimal.Round((income - expenses) / income * 100m, 1) : 0m;

        return new SummaryDto(code, start, end, income, expenses, income - expenses, savingsRate, balance, totals.Sum(t => t.Count));
    }

    public async Task<IReadOnlyList<CategoryBreakdownDto>> GetCategoryBreakdownAsync(
        DateOnly? from, DateOnly? to, string? currency, CategoryKind kind, CancellationToken ct)
    {
        var (start, end) = ResolvePeriod(from, to);
        var code = await ResolveCurrencyAsync(currency, ct);
        var type = kind == CategoryKind.Income ? TransactionType.Income : TransactionType.Expense;

        var groups = await IncomeAndExpenses(start, end, code)
            .Where(t => t.Type == type)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var ids = groups.Where(g => g.CategoryId != null).Select(g => g.CategoryId!.Value).ToList();
        var categories = await db.Categories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var grandTotal = groups.Sum(g => g.Total);

        return groups
            .Select(g =>
            {
                var category = g.CategoryId is { } id ? categories.GetValueOrDefault(id) : null;
                return new CategoryBreakdownDto(
                    g.CategoryId,
                    category?.Name ?? "Uncategorised",
                    category?.Color ?? UncategorisedColor,
                    g.Total,
                    grandTotal == 0 ? 0 : decimal.Round(g.Total / grandTotal * 100m, 1),
                    g.Count);
            })
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    /// <summary>Monthly income and expenses for the last <paramref name="months"/> months, including empty months.</summary>
    public async Task<IReadOnlyList<CashFlowPointDto>> GetCashFlowAsync(int months, string? currency, CancellationToken ct)
    {
        if (months is < 1 or > 36)
        {
            throw new ValidationException([new ValidationFailure("months", "Months must be between 1 and 36.")]);
        }

        var code = await ResolveCurrencyAsync(currency, ct);
        var today = Today();
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));
        var lastDay = new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1);

        var rows = await IncomeAndExpenses(firstMonth, lastDay, code)
            .GroupBy(t => new { t.Date.Year, t.Date.Month, t.Type })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        return Enumerable.Range(0, months)
            .Select(i => firstMonth.AddMonths(i))
            .Select(m =>
            {
                var income = rows.Where(r => r.Year == m.Year && r.Month == m.Month && r.Type == TransactionType.Income).Sum(r => r.Total);
                var expenses = rows.Where(r => r.Year == m.Year && r.Month == m.Month && r.Type == TransactionType.Expense).Sum(r => r.Total);
                return new CashFlowPointDto(m.Year, m.Month, income, expenses, income - expenses);
            })
            .ToList();
    }

    private IQueryable<Transaction> IncomeAndExpenses(DateOnly start, DateOnly end, string currency) =>
        db.Transactions.AsNoTracking().Where(t =>
            t.UserId == currentUser.Id
            && (t.Type == TransactionType.Income || t.Type == TransactionType.Expense)
            && t.Account.Currency == currency
            && t.Date >= start
            && t.Date <= end);

    private (DateOnly Start, DateOnly End) ResolvePeriod(DateOnly? from, DateOnly? to)
    {
        var today = Today();
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);

        if (end < start)
        {
            throw new ValidationException([new ValidationFailure("to", "'to' must be on or after 'from'.")]);
        }

        if (end.DayNumber - start.DayNumber > MaxRangeDays)
        {
            throw new ValidationException([new ValidationFailure("to", "The reporting period can be at most 10 years.")]);
        }

        return (start, end);
    }

    private async Task<string> ResolveCurrencyAsync(string? currency, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return await db.Users.Where(u => u.Id == currentUser.Id).Select(u => u.BaseCurrency).SingleAsync(ct);
        }

        var code = currency.Trim().ToUpperInvariant();
        if (!Money.IsSupportedCurrency(code))
        {
            throw new ValidationException([new ValidationFailure("currency", $"Currency '{currency}' is not supported.")]);
        }

        return code;
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
