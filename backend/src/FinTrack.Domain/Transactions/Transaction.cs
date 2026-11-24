using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Common;

namespace FinTrack.Domain.Transactions;

public enum TransactionType
{
    Income = 0,
    Expense = 1,
    TransferIn = 2,
    TransferOut = 3,
}

/// <summary>
/// A single posting against an account. Amounts are always stored as positive values;
/// the direction comes from <see cref="Type"/>. Every factory and mutator keeps the
/// owning account's running balance in sync, so the balance can never drift from the
/// sum of its postings.
/// </summary>
public sealed class Transaction : Entity, IAuditable
{
    public const int DescriptionMaxLength = 200;
    public const int NotesMaxLength = 1000;

    private Transaction()
    {
        // Required by EF Core.
    }

    private Transaction(
        Account account,
        TransactionType type,
        decimal amount,
        DateOnly date,
        string description,
        Guid? categoryId,
        string? notes,
        Guid? transferId)
    {
        UserId = account.UserId;
        TransferId = transferId;
        SetValues(account.Id, type, amount, date, description, categoryId, notes);
    }

    public Guid UserId { get; private set; }

    public Guid AccountId { get; private set; }

    public Account Account { get; private set; } = null!;

    public Guid? CategoryId { get; private set; }

    public Category? Category { get; private set; }

    public TransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly Date { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    /// <summary>Links the two legs of a transfer between accounts.</summary>
    public Guid? TransferId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsTransfer => TransferId is not null;

    /// <summary>The amount as it affects the account balance (+ inflow, - outflow).</summary>
    public decimal SignedAmount => Type is TransactionType.Income or TransactionType.TransferIn ? Amount : -Amount;

    /// <summary>Records income or an expense and updates the account balance.</summary>
    public static Transaction Record(
        Account account,
        TransactionType type,
        decimal amount,
        DateOnly date,
        string description,
        Category? category,
        string? notes = null)
    {
        EnsureIncomeOrExpense(type);
        EnsureCategoryFits(category, type, account.UserId);

        var transaction = new Transaction(account, type, amount, date, description, category?.Id, notes, transferId: null);
        account.Post(transaction.SignedAmount);
        return transaction;
    }

    /// <summary>Moves money between two accounts of the same owner and currency.</summary>
    public static (Transaction Outgoing, Transaction Incoming) CreateTransfer(
        Account from,
        Account to,
        decimal amount,
        DateOnly date,
        string description)
    {
        if (from.Id == to.Id)
        {
            throw new DomainException("Source and destination accounts must be different.");
        }

        if (from.UserId != to.UserId)
        {
            throw new DomainException("Transfers are only allowed between your own accounts.");
        }

        if (from.Currency != to.Currency)
        {
            throw new DomainException("Transfers between accounts with different currencies are not supported.");
        }

        var transferId = Guid.CreateVersion7();
        var outgoing = new Transaction(from, TransactionType.TransferOut, amount, date, description, null, null, transferId);
        var incoming = new Transaction(to, TransactionType.TransferIn, amount, date, description, null, null, transferId);

        from.Post(outgoing.SignedAmount);
        to.Post(incoming.SignedAmount);

        return (outgoing, incoming);
    }

    /// <summary>
    /// Changes an income or expense, moving its effect from <paramref name="currentAccount"/>
    /// to <paramref name="targetAccount"/> (which may be the same account).
    /// </summary>
    public void Amend(
        Account currentAccount,
        Account targetAccount,
        TransactionType type,
        decimal amount,
        DateOnly date,
        string description,
        Category? category,
        string? notes)
    {
        if (IsTransfer)
        {
            throw new DomainException("Transfers cannot be edited. Delete the transfer and record it again.");
        }

        if (currentAccount.Id != AccountId)
        {
            throw new ArgumentException("The current account does not own this transaction.", nameof(currentAccount));
        }

        if (targetAccount.UserId != UserId)
        {
            throw new DomainException("Transactions can only be moved to your own accounts.");
        }

        EnsureIncomeOrExpense(type);
        EnsureCategoryFits(category, type, UserId);

        currentAccount.Post(-SignedAmount);
        SetValues(targetAccount.Id, type, amount, date, description, category?.Id, notes);
        targetAccount.Post(SignedAmount);
    }

    /// <summary>Removes this posting's effect from the account balance. Call before deleting.</summary>
    public void Reverse(Account account)
    {
        if (account.Id != AccountId)
        {
            throw new ArgumentException("The account does not own this transaction.", nameof(account));
        }

        account.Post(-SignedAmount);
    }

    private static void EnsureIncomeOrExpense(TransactionType type)
    {
        if (type is not (TransactionType.Income or TransactionType.Expense))
        {
            throw new DomainException("Use a transfer to move money between accounts.");
        }
    }

    private static void EnsureCategoryFits(Category? category, TransactionType type, Guid userId)
    {
        if (category is null)
        {
            return;
        }

        if (category.UserId != userId)
        {
            throw new DomainException("Category does not belong to this user.");
        }

        if (!category.Accepts(type))
        {
            throw new DomainException($"Category '{category.Name}' cannot be used for {type.ToString().ToLowerInvariant()} transactions.");
        }
    }

    private void SetValues(
        Guid accountId,
        TransactionType type,
        decimal amount,
        DateOnly date,
        string description,
        Guid? categoryId,
        string? notes)
    {
        if (!Money.IsValidPositiveAmount(amount))
        {
            throw new DomainException("Amount must be positive, within range and have at most two decimal places.");
        }

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > DescriptionMaxLength)
        {
            throw new DomainException($"Description is required and must be at most {DescriptionMaxLength} characters.");
        }

        if (notes is not null && notes.Length > NotesMaxLength)
        {
            throw new DomainException($"Notes must be at most {NotesMaxLength} characters.");
        }

        AccountId = accountId;
        Type = type;
        Amount = amount;
        Date = date;
        Description = description.Trim();
        CategoryId = categoryId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
