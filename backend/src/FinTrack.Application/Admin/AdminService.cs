using FinTrack.Application.Abstractions;
using FinTrack.Application.Common;
using FinTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FinTrack.Application.Admin;

public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    int AccountCount,
    int TransactionCount);

public sealed record PlatformStatsDto(int Users, int ActiveUsersLast30Days, int Accounts, int Transactions);

/// <summary>Operational views for administrators. Exposes counts only, never users' financial details.</summary>
public sealed class AdminService(IAppDbContext db, TimeProvider clock)
{
    public async Task<PagedResult<AdminUserDto>> ListUsersAsync(string? search, int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.Email.Contains(term) || u.FullName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.CreatedAt,
                u.LastLoginAt,
                db.Accounts.Count(a => a.UserId == u.Id),
                db.Transactions.Count(t => t.UserId == u.Id)))
            .ToListAsync(ct);

        return new PagedResult<AdminUserDto>(items, p, size, total);
    }

    public async Task<PlatformStatsDto> GetStatsAsync(CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-30);
        return new PlatformStatsDto(
            await db.Users.CountAsync(ct),
            await db.Users.CountAsync(u => u.LastLoginAt >= since, ct),
            await db.Accounts.CountAsync(ct),
            await db.Transactions.CountAsync(ct));
    }
}
