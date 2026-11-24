using FinTrack.Domain.Common;

namespace FinTrack.Domain.Accounts;

public enum AccountType
{
    Checking = 0,
    Savings = 1,
    CreditCard = 2,
    Cash = 3,
    Investment = 4,
}

/// <summary>
/// A financial account (bank account, card, wallet). The running balance is kept on the
/// aggregate and only changes through <see cref="Post"/>, which is called by
/// <see cref="Transactions.Transaction"/> when money is recorded, amended or reversed.
/// Concurrent updates are detected with an optimistic concurrency token (<see cref="Version"/>).
/// </summary>
public sealed class Account : Entity, IAuditable
{
    public const int NameMaxLength = 100;

    private Account()
    {
        // Required by EF Core.
    }

    public Account(Guid userId, string name, AccountType type, string currency, decimal openingBalance)
    {
        if (decimal.Round(openingBalance, 2) != openingBalance || Math.Abs(openingBalance) > Money.MaxAmount)
        {
            throw new DomainException("Opening balance must have at most two decimal places and be within range.");
        }

        UserId = userId;
        SetDetails(name, type);
        Currency = Money.NormalizeCurrency(currency);
        OpeningBalance = openingBalance;
        Balance = openingBalance;
    }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AccountType Type { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal OpeningBalance { get; private set; }

    public decimal Balance { get; private set; }

    public bool IsArchived { get; private set; }

    /// <summary>Concurrency token, mapped to PostgreSQL's <c>xmin</c> system column.</summary>
    public uint Version { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public void Update(string name, AccountType type) => SetDetails(name, type);

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;

    /// <summary>Applies a signed amount to the running balance.</summary>
    internal void Post(decimal signedAmount)
    {
        if (IsArchived)
        {
            throw new DomainException($"Account '{Name}' is archived and cannot be changed.");
        }

        var newBalance = Balance + signedAmount;
        if (Math.Abs(newBalance) > Money.MaxAmount)
        {
            throw new DomainException("The resulting balance is out of range.");
        }

        Balance = newBalance;
    }

    private void SetDetails(string name, AccountType type)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            throw new DomainException($"Account name is required and must be at most {NameMaxLength} characters.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Unknown account type.");
        }

        Name = name.Trim();
        Type = type;
    }
}
