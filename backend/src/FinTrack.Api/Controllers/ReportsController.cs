using FinTrack.Application.Admin;
using FinTrack.Application.Common;
using FinTrack.Application.Reports;
using FinTrack.Domain.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Income, expenses, net and savings rate for a period (defaults to the current month).</summary>
    [HttpGet("summary")]
    public Task<SummaryDto> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? currency, CancellationToken ct) =>
        reports.GetSummaryAsync(from, to, currency, ct);

    /// <summary>Totals per category for a period.</summary>
    [HttpGet("categories")]
    public Task<IReadOnlyList<CategoryBreakdownDto>> Categories(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? currency,
        [FromQuery] CategoryKind kind,
        CancellationToken ct) =>
        reports.GetCategoryBreakdownAsync(from, to, currency, kind, ct);

    /// <summary>Monthly income and expenses for the last N months.</summary>
    [HttpGet("cash-flow")]
    public Task<IReadOnlyList<CashFlowPointDto>> CashFlow([FromQuery] int months = 12, [FromQuery] string? currency = null, CancellationToken ct = default) =>
        reports.GetCashFlowAsync(months, currency, ct);
}

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(AdminService admin) : ControllerBase
{
    [HttpGet("users")]
    public Task<PagedResult<AdminUserDto>> Users([FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        admin.ListUsersAsync(search, page, pageSize, ct);

    [HttpGet("stats")]
    public Task<PlatformStatsDto> Stats(CancellationToken ct) => admin.GetStatsAsync(ct);
}
