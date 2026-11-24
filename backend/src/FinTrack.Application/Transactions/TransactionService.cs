using FinTrack.Application.Abstractions;
using FinTrack.Application.Common;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Transactions;

public sealed class TransactionService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<SaveTransactionRequest> saveValidator,
    IValidator<CreateTransferRequest> transferValidator,
    IValidator<TransactionQuery> queryValidator)
{
    /// <summary>Maximum number of rows a single CSV export returns.</summary>
    public const int ExportLimit = 10_000;

    public async Task<PagedResult<TransactionDto>> SearchAsync(TransactionQuery query, CancellationToken ct)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var filtered = ApplyFilter(db.Transactions.AsNoTracking(), query);
        var total = await filtered.CountAsync(ct);
        var items = await ApplySort(filtered, query.Sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(TransactionMapping.Projection)
            .ToListAsync(ct);

        return new PagedResult<TransactionDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<TransactionDto>> ExportAsync(TransactionQuery query, CancellationToken ct)
    {
        await queryValidator.ValidateAndThrowAsync(query, ct);
        return await ApplySort(ApplyFilter(db.Transactions.AsNoTracking(), query), query.Sort)
            .Take(ExportLimit)
            .Select(TransactionMapping.Projection)
            .ToListAsync(ct);
    }

    public async Task<TransactionDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Transactions.AsNoTracking()
            .Where(t => t.Id == id && t.UserId == currentUser.Id)
            .Select(TransactionMapping.Projection)
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Transaction", id);

    public async Task<TransactionDto> CreateAsync(SaveTransactionRequest request, CancellationToken ct)
    {
        await saveValidator.ValidateAndThrowAsync(request, ct);

        var account = await FindAccountAsync(request.AccountId, ct);
        var category = await FindCategoryAsync(request.CategoryId, ct);

        var transaction = Transaction.Record(
            account, request.Type, request.Amount, request.Date, request.Description, category, request.Notes);

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);
        return transaction.ToDto(account, category);
    }

    public async Task<TransactionDto> UpdateAsync(Guid id, SaveTransactionRequest request, CancellationToken ct)
    {
        await saveValidator.ValidateAndThrowAsync(request, ct);

        var transaction = await FindTransactionAsync(id, ct);
        var currentAccount = await FindAccountAsync(transaction.AccountId, ct);
        var targetAccount = request.AccountId == currentAccount.Id
            ? currentAccount
            : await FindAccountAsync(request.AccountId, ct);
        var category = await FindCategoryAsync(request.CategoryId, ct);

        transaction.Amend(
            currentAccount, targetAccount, request.Type, request.Amount, request.Date, request.Description, category, request.Notes);

        await db.SaveChangesAsync(ct);
        return transaction.ToDto(targetAccount, category);
    }

    /// <summary>Deletes a transaction. Deleting either leg of a transfer removes both legs.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var transaction = await FindTransactionAsync(id, ct);

        var postings = transaction.TransferId is { } transferId
            ? await db.Transactions.Where(t => t.TransferId == transferId && t.UserId == currentUser.Id).ToListAsync(ct)
            : [transaction];

        foreach (var posting in postings)
        {
            var account = await FindAccountAsync(posting.AccountId, ct);
            posting.Reverse(account);
            db.Transactions.Remove(posting);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<TransferDto> CreateTransferAsync(CreateTransferRequest request, CancellationToken ct)
    {
        await transferValidator.ValidateAndThrowAsync(request, ct);

        var from = await FindAccountAsync(request.FromAccountId, ct);
        var to = await FindAccountAsync(request.ToAccountId, ct);

        var (outgoing, incoming) = Transaction.CreateTransfer(from, to, request.Amount, request.Date, request.Description);
        db.Transactions.AddRange(outgoing, incoming);
        await db.SaveChangesAsync(ct);

        return new TransferDto(outgoing.TransferId!.Value, outgoing.ToDto(from, null), incoming.ToDto(to, null));
    }

    private IQueryable<Transaction> ApplyFilter(IQueryable<Transaction> source, TransactionQuery query)
    {
        var result = source.Where(t => t.UserId == currentUser.Id);

        if (query.From is { } from)
        {
            result = result.Where(t => t.Date >= from);
        }

        if (query.To is { } to)
        {
            result = result.Where(t => t.Date <= to);
        }

        if (query.AccountId is { } accountId)
        {
            result = result.Where(t => t.AccountId == accountId);
        }

        if (query.CategoryId is { } categoryId)
        {
            result = result.Where(t => t.CategoryId == categoryId);
        }

        if (query.Type is { } type)
        {
            result = result.Where(t => t.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            result = result.Where(t =>
                t.Description.ToLower().Contains(term) ||
                (t.Notes != null && t.Notes.ToLower().Contains(term)));
        }

        return result;
    }

    private static IQueryable<Transaction> ApplySort(IQueryable<Transaction> source, TransactionSort sort) => sort switch
    {
        TransactionSort.DateAsc => source.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt),
        TransactionSort.AmountDesc => source.OrderByDescending(t => t.Amount).ThenByDescending(t => t.Date),
        TransactionSort.AmountAsc => source.OrderBy(t => t.Amount).ThenByDescending(t => t.Date),
        _ => source.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt),
    };

    private async Task<Transaction> FindTransactionAsync(Guid id, CancellationToken ct) =>
        await db.Transactions.SingleOrDefaultAsync(t => t.Id == id && t.UserId == currentUser.Id, ct)
        ?? throw new NotFoundException("Transaction", id);

    private async Task<Account> FindAccountAsync(Guid id, CancellationToken ct) =>
        await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == currentUser.Id, ct)
        ?? throw new NotFoundException("Account", id);

    private async Task<Category?> FindCategoryAsync(Guid? id, CancellationToken ct)
    {
        if (id is null)
        {
            return null;
        }

        return await db.Categories.SingleOrDefaultAsync(c => c.Id == id && c.UserId == currentUser.Id, ct)
            ?? throw new NotFoundException("Category", id);
    }
}
