using System.Linq.Expressions;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;
using FluentValidation;

namespace FinTrack.Application.Transactions;

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Currency,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryColor,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    string? Notes,
    Guid? TransferId,
    DateTimeOffset CreatedAt);

public sealed record TransferDto(Guid TransferId, TransactionDto Outgoing, TransactionDto Incoming);

public sealed record SaveTransactionRequest(
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    DateOnly Date,
    string Description,
    Guid? CategoryId,
    string? Notes);

public sealed record CreateTransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateOnly Date,
    string Description);

public enum TransactionSort
{
    DateDesc = 0,
    DateAsc = 1,
    AmountDesc = 2,
    AmountAsc = 3,
}

/// <summary>Filter, sort and paging options for listing or exporting transactions.</summary>
public sealed class TransactionQuery
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public Guid? AccountId { get; init; }

    public Guid? CategoryId { get; init; }

    public TransactionType? Type { get; init; }

    public string? Search { get; init; }

    public TransactionSort Sort { get; init; } = TransactionSort.DateDesc;

    public int? Page { get; init; }

    public int? PageSize { get; init; }
}

public sealed class SaveTransactionRequestValidator : AbstractValidator<SaveTransactionRequest>
{
    public SaveTransactionRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Type)
            .Must(t => t is TransactionType.Income or TransactionType.Expense)
            .WithMessage("Type must be Income or Expense. Use the transfers endpoint to move money between accounts.");
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(Money.MaxAmount)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Date).SetValidator(new PostingDateValidator(clock));
        RuleFor(x => x.Description).NotEmpty().MaximumLength(Transaction.DescriptionMaxLength);
        RuleFor(x => x.Notes).MaximumLength(Transaction.NotesMaxLength);
    }
}

public sealed class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x.FromAccountId).NotEmpty();
        RuleFor(x => x.ToAccountId).NotEmpty().NotEqual(x => x.FromAccountId)
            .WithMessage("Source and destination accounts must be different.");
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(Money.MaxAmount)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Date).SetValidator(new PostingDateValidator(clock));
        RuleFor(x => x.Description).NotEmpty().MaximumLength(Transaction.DescriptionMaxLength);
    }
}

public sealed class TransactionQueryValidator : AbstractValidator<TransactionQuery>
{
    public TransactionQueryValidator()
    {
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("'To' must be on or after 'From'.");
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type is not null);
        RuleFor(x => x.Sort).IsInEnum();
    }
}

/// <summary>Posting dates must be realistic: not before 2000 and at most one year ahead.</summary>
internal sealed class PostingDateValidator : AbstractValidator<DateOnly>
{
    public PostingDateValidator(TimeProvider clock)
    {
        RuleFor(d => d)
            .Must(d =>
            {
                var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
                return d >= new DateOnly(2000, 1, 1) && d <= today.AddYears(1);
            })
            .OverridePropertyName("Date")
            .WithMessage("Date must be between 2000-01-01 and one year from today.");
    }
}

internal static class TransactionMapping
{
    public static readonly Expression<Func<Transaction, TransactionDto>> Projection = t => new TransactionDto(
        t.Id,
        t.AccountId,
        t.Account.Name,
        t.Account.Currency,
        t.CategoryId,
        t.Category != null ? t.Category.Name : null,
        t.Category != null ? t.Category.Color : null,
        t.Type,
        t.Amount,
        t.Date,
        t.Description,
        t.Notes,
        t.TransferId,
        t.CreatedAt);

    public static TransactionDto ToDto(this Transaction t, Account account, Category? category) => new(
        t.Id,
        account.Id,
        account.Name,
        account.Currency,
        category?.Id,
        category?.Name,
        category?.Color,
        t.Type,
        t.Amount,
        t.Date,
        t.Description,
        t.Notes,
        t.TransferId,
        t.CreatedAt);
}
