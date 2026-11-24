using FinTrack.Application.Budgets;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

public sealed record CopyBudgetsResponse(int Copied);

[ApiController]
[Route("api/budgets")]
public sealed class BudgetsController(BudgetService budgets, TimeProvider clock) : ControllerBase
{
    /// <summary>Budgets for a month (defaults to the current month) with spending progress.</summary>
    [HttpGet]
    public Task<IReadOnlyList<BudgetDto>> List([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        budgets.ListAsync(Period(year, month), ct);

    /// <summary>Creates or updates the limit for a category and month.</summary>
    [HttpPut]
    public Task<BudgetDto> Upsert(UpsertBudgetRequest request, CancellationToken ct) => budgets.UpsertAsync(request, ct);

    [HttpPost("copy-previous")]
    public async Task<CopyBudgetsResponse> CopyPrevious([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct) =>
        new(await budgets.CopyFromPreviousMonthAsync(Period(year, month), ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await budgets.DeleteAsync(id, ct);
        return NoContent();
    }

    private BudgetPeriod Period(int? year, int? month)
    {
        var now = clock.GetUtcNow();
        return new BudgetPeriod(year ?? now.Year, month ?? now.Month);
    }
}
