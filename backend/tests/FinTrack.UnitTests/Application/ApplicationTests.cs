using FinTrack.Application.Auth;
using FinTrack.Application.Common;
using FinTrack.Application.Transactions;
using FinTrack.Domain.Transactions;
using Microsoft.Extensions.Time.Testing;

namespace FinTrack.UnitTests.Application;

public sealed class CsvExporterTests
{
    private static TransactionDto Row(string description, TransactionType type = TransactionType.Expense, decimal amount = 12.5m, string? notes = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Chequing", "CAD", null, "Groceries", "#22C55E", type, amount, new DateOnly(2026, 11, 3), description, notes, null, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Writes_a_header_and_signed_amounts()
    {
        var csv = CsvExporter.Write([Row("Milk"), Row("Salary", TransactionType.Income, 3_000m)]);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Date,Type,Description,Category,Account,Currency,Amount,Notes", lines[0]);
        Assert.Equal("2026-11-03,Expense,Milk,Groceries,Chequing,CAD,-12.50,", lines[1]);
        Assert.Equal("2026-11-03,Income,Salary,Groceries,Chequing,CAD,3000.00,", lines[2]);
    }

    [Theory]
    [InlineData("Coffee, large", "\"Coffee, large\"")]
    [InlineData("The \"best\" pizza", "\"The \"\"best\"\" pizza\"")]
    [InlineData("Line\nbreak", "\"Line\nbreak\"")]
    public void Quotes_fields_that_need_it(string input, string expected)
    {
        Assert.Equal(expected, CsvExporter.Text(input));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void Neutralises_spreadsheet_formulas(string input)
    {
        Assert.StartsWith("'", CsvExporter.Text(input).TrimStart('"'), StringComparison.Ordinal);
    }
}

public sealed class ValidatorTests
{
    [Theory]
    [InlineData("short1A", false)]
    [InlineData("alllowercase1", false)]
    [InlineData("ALLUPPERCASE1", false)]
    [InlineData("NoDigitsHere", false)]
    [InlineData("Valid1Password", true)]
    public void Register_enforces_password_policy(string password, bool expected)
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("a@b.com", password, "Alex", "CAD"));

        Assert.Equal(expected, result.IsValid);
    }

    [Fact]
    public void Register_rejects_unsupported_currency()
    {
        var result = new RegisterRequestValidator().Validate(new RegisterRequest("a@b.com", "Valid1Password", "Alex", "BTC"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.BaseCurrency));
    }

    [Fact]
    public void Transaction_date_cannot_be_more_than_a_year_ahead()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));
        var validator = new SaveTransactionRequestValidator(clock);

        var ok = validator.Validate(Request(new DateOnly(2027, 11, 15)));
        var tooFar = validator.Validate(Request(new DateOnly(2027, 11, 16)));

        Assert.True(ok.IsValid);
        Assert.False(tooFar.IsValid);
    }

    [Fact]
    public void Transaction_amount_must_have_at_most_two_decimals()
    {
        var validator = new SaveTransactionRequestValidator(TimeProvider.System);

        var result = validator.Validate(Request(DateOnly.FromDateTime(DateTime.UtcNow)) with { Amount = 1.999m });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SaveTransactionRequest.Amount));
    }

    [Fact]
    public void Transfer_requires_two_different_accounts()
    {
        var id = Guid.NewGuid();
        var validator = new CreateTransferRequestValidator(TimeProvider.System);

        var result = validator.Validate(new CreateTransferRequest(id, id, 10m, DateOnly.FromDateTime(DateTime.UtcNow), "Move"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTransferRequest.ToAccountId));
    }

    [Theory]
    [InlineData(null, null, 1, 25)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(3, 500, 3, 100)]
    public void Paging_is_clamped(int? page, int? size, int expectedPage, int expectedSize)
    {
        Assert.Equal((expectedPage, expectedSize), Paging.Normalize(page, size));
    }

    private static SaveTransactionRequest Request(DateOnly date) =>
        new(Guid.NewGuid(), TransactionType.Expense, 10m, date, "Lunch", null, null);
}
