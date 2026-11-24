using FinTrack.Application.Accounts;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController(AccountService accounts) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AccountDto>> List([FromQuery] bool includeArchived, CancellationToken ct) =>
        accounts.ListAsync(includeArchived, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<AccountDto> Get(Guid id, CancellationToken ct) => accounts.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<AccountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken ct)
    {
        var account = await accounts.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = account.Id }, account);
    }

    [HttpPut("{id:guid}")]
    public Task<AccountDto> Update(Guid id, UpdateAccountRequest request, CancellationToken ct) =>
        accounts.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/archive")]
    public Task<AccountDto> Archive(Guid id, CancellationToken ct) => accounts.SetArchivedAsync(id, archived: true, ct);

    [HttpPost("{id:guid}/restore")]
    public Task<AccountDto> Restore(Guid id, CancellationToken ct) => accounts.SetArchivedAsync(id, archived: false, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await accounts.DeleteAsync(id, ct);
        return NoContent();
    }
}
