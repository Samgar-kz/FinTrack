using FinTrack.Application.Abstractions;
using FinTrack.Application.Common;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Accounts;

public sealed record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string Currency,
    decimal OpeningBalance,
    decimal Balance,
    bool IsArchived,
    DateTimeOffset CreatedAt);

public sealed record CreateAccountRequest(string Name, AccountType Type, string Currency, decimal OpeningBalance);

public sealed record UpdateAccountRequest(string Name, AccountType Type);

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Account.NameMaxLength);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Currency)
            .Must(Money.IsSupportedCurrency)
            .WithMessage($"Currency must be one of: {string.Join(", ", Money.SupportedCurrencies)}.");
        RuleFor(x => x.OpeningBalance)
            .InclusiveBetween(-Money.MaxAmount, Money.MaxAmount)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Account.NameMaxLength);
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class AccountService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<CreateAccountRequest> createValidator,
    IValidator<UpdateAccountRequest> updateValidator)
{
    public async Task<IReadOnlyList<AccountDto>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        var query = db.Accounts.AsNoTracking().Where(a => a.UserId == currentUser.Id);
        if (!includeArchived)
        {
            query = query.Where(a => !a.IsArchived);
        }

        return await query
            .OrderBy(a => a.IsArchived)
            .ThenBy(a => a.Name)
            .Select(a => new AccountDto(a.Id, a.Name, a.Type, a.Currency, a.OpeningBalance, a.Balance, a.IsArchived, a.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<AccountDto> GetAsync(Guid id, CancellationToken ct) =>
        ToDto(await FindAsync(id, ct));

    public async Task<AccountDto> CreateAsync(CreateAccountRequest request, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);
        await EnsureNameIsFreeAsync(request.Name, excludeId: null, ct);

        var account = new Account(currentUser.Id, request.Name, request.Type, request.Currency, request.OpeningBalance);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    public async Task<AccountDto> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        var account = await FindAsync(id, ct);
        await EnsureNameIsFreeAsync(request.Name, account.Id, ct);

        account.Update(request.Name, request.Type);
        await db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    public async Task<AccountDto> SetArchivedAsync(Guid id, bool archived, CancellationToken ct)
    {
        var account = await FindAsync(id, ct);
        if (archived)
        {
            account.Archive();
        }
        else
        {
            account.Restore();
        }

        await db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    /// <summary>Deletes an account that has no transactions. Accounts with history must be archived instead.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var account = await FindAsync(id, ct);
        if (await db.Transactions.AnyAsync(t => t.AccountId == account.Id, ct))
        {
            throw new ConflictException("This account has transactions. Archive it instead of deleting it.");
        }

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
    }

    private static AccountDto ToDto(Account a) =>
        new(a.Id, a.Name, a.Type, a.Currency, a.OpeningBalance, a.Balance, a.IsArchived, a.CreatedAt);

    private async Task<Account> FindAsync(Guid id, CancellationToken ct) =>
        await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.Id, ct)
        ?? throw new NotFoundException("Account", id);

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        var taken = await db.Accounts.AnyAsync(
            a => a.UserId == currentUser.Id && a.Id != excludeId && a.Name.ToLower() == normalized,
            ct);

        if (taken)
        {
            throw new ConflictException($"You already have an account named '{name.Trim()}'.");
        }
    }
}
