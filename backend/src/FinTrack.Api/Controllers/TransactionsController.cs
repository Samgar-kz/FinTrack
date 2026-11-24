using System.Text;
using FinTrack.Application.Common;
using FinTrack.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace FinTrack.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(TransactionService transactions, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<TransactionDto>> Search([FromQuery] TransactionQuery query, CancellationToken ct) =>
        transactions.SearchAsync(query, ct);

    /// <summary>Downloads the filtered transactions as CSV (max 10,000 rows).</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    public async Task<IActionResult> Export([FromQuery] TransactionQuery query, CancellationToken ct)
    {
        var rows = await transactions.ExportAsync(query, ct);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(CsvExporter.Write(rows))).ToArray();
        var fileName = $"fintrack-transactions-{clock.GetUtcNow():yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<TransactionDto> Get(Guid id, CancellationToken ct) => transactions.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(SaveTransactionRequest request, CancellationToken ct)
    {
        var transaction = await transactions.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = transaction.Id }, transaction);
    }

    [HttpPut("{id:guid}")]
    public Task<TransactionDto> Update(Guid id, SaveTransactionRequest request, CancellationToken ct) =>
        transactions.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await transactions.DeleteAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/transfers")]
public sealed class TransfersController(TransactionService transactions) : ControllerBase
{
    /// <summary>Moves money between two of the caller's accounts in the same currency.</summary>
    [HttpPost]
    [ProducesResponseType<TransferDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateTransferRequest request, CancellationToken ct)
    {
        var transfer = await transactions.CreateTransferAsync(request, ct);
        return Created($"/api/transactions/{transfer.Outgoing.Id}", transfer);
    }
}
