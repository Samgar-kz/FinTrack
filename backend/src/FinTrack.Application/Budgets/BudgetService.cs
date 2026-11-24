using FinTrack.Application.Abstractions;
using FinTrack.Application.Common;
using FinTrack.Domain.Budgets;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Budgets;

public sealed record BudgetDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string CategoryColor,
    int Year,
    int Month,
    string Currency,
    decimal Limit,
    decimal Spent,
    decimal Remaining,
    decimal PercentUsed);

public sealed record UpsertBudgetRequest(Guid CategoryId, int Year, int Month, decimal Limit);

public sealed record BudgetPeriod(int Year, int Month);

public sealed class UpsertBudgetRequestValidator : AbstractValidator<UpsertBudgetRequest>
{
    public UpsertBudgetRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Limit)
            .GreaterThan(0)
            .LessThanOrEqualTo(Money.MaxAmount)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}

public sealed class BudgetPeriodValidator : AbstractValidator<BudgetPeriod>
{
    public BudgetPeriodValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class BudgetService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<UpsertBudgetRequest> upsertValidator,
    IValidator<BudgetPeriod> periodValidator)
{
    /// <summary>Budgets for a month together with what has been spent against each one.</summary>
    public async Task<IReadOnlyList<BudgetDto>> ListAsync(BudgetPeriod period, CancellationToken ct)
    {
        await periodValidator.ValidateAndThrowAsync(period, ct);
        var start = new DateOnly(period.Year, period.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var budgets = await db.Budgets.AsNoTracking()
            .Where(b => b.UserId == currentUser.Id && b.Year == period.Year && b.Month == period.Month)
            .Select(b => new { b.Id, b.CategoryId, b.Category.Name, b.Category.Color, b.Currency, b.Limit })
            .ToListAsync(ct);

        var spent = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == currentUser.Id
                        && t.Type == TransactionType.Expense
                        && t.CategoryId != null
                        && t.Date >= start && t.Date <= end)
            .GroupBy(t => new { t.CategoryId, t.Account.Currency })
            .Select(g => new { g.Key.CategoryId, g.Key.Currency, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        return budgets
            .Select(b =>
            {
                var used = spent.FirstOrDefault(s => s.CategoryId == b.CategoryId && s.Currency == b.Currency)?.Total ?? 0m;
                return new BudgetDto(
                    b.Id,
                    b.CategoryId,
                    b.Name,
                    b.Color,
                    period.Year,
                    period.Month,
                    b.Currency,
                    b.Limit,
                    used,
                    b.Limit - used,
                    decimal.Round(used / b.Limit * 100m, 1));
            })
            .OrderByDescending(b => b.PercentUsed)
            .ThenBy(b => b.CategoryName)
            .ToList();
    }

    /// <summary>Creates or updates the budget for a category and month, in the user's base currency.</summary>
    public async Task<BudgetDto> UpsertAsync(UpsertBudgetRequest request, CancellationToken ct)
    {
        await upsertValidator.ValidateAndThrowAsync(request, ct);

        var category = await db.Categories.SingleOrDefaultAsync(
                c => c.Id == request.CategoryId && c.UserId == currentUser.Id, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var budget = await db.Budgets.SingleOrDefaultAsync(
            b => b.UserId == currentUser.Id
                 && b.CategoryId == category.Id
                 && b.Year == request.Year
                 && b.Month == request.Month,
            ct);

        if (budget is null)
        {
            var baseCurrency = await db.Users
                .Where(u => u.Id == currentUser.Id)
                .Select(u => u.BaseCurrency)
                .SingleAsync(ct);

            budget = new Budget(category, request.Year, request.Month, baseCurrency, request.Limit);
            db.Budgets.Add(budget);
        }
        else
        {
            budget.ChangeLimit(request.Limit);
        }

        await db.SaveChangesAsync(ct);

        return (await ListAsync(new BudgetPeriod(request.Year, request.Month), ct)).Single(b => b.Id == budget.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var budget = await db.Budgets.SingleOrDefaultAsync(b => b.Id == id && b.UserId == currentUser.Id, ct)
            ?? throw new NotFoundException("Budget", id);

        db.Budgets.Remove(budget);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Copies the previous month's budgets into the given month, skipping categories already budgeted.</summary>
    public async Task<int> CopyFromPreviousMonthAsync(BudgetPeriod period, CancellationToken ct)
    {
        await periodValidator.ValidateAndThrowAsync(period, ct);
        var previous = new DateOnly(period.Year, period.Month, 1).AddMonths(-1);

        var source = await db.Budgets
            .Include(b => b.Category)
            .Where(b => b.UserId == currentUser.Id && b.Year == previous.Year && b.Month == previous.Month)
            .ToListAsync(ct);

        var alreadyBudgeted = await db.Budgets
            .Where(b => b.UserId == currentUser.Id && b.Year == period.Year && b.Month == period.Month)
            .Select(b => b.CategoryId)
            .ToListAsync(ct);

        var copies = source
            .Where(b => !alreadyBudgeted.Contains(b.CategoryId) && b.Category.Kind == CategoryKind.Expense)
            .Select(b => new Budget(b.Category, period.Year, period.Month, b.Currency, b.Limit))
            .ToList();

        db.Budgets.AddRange(copies);
        await db.SaveChangesAsync(ct);
        return copies.Count;
    }
}
