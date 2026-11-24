using System.Globalization;
using System.Text;

namespace FinTrack.Application.Transactions;

/// <summary>Writes transactions as RFC 4180 CSV, hardened against spreadsheet formula injection.</summary>
public static class CsvExporter
{
    private static readonly string[] Header =
        ["Date", "Type", "Description", "Category", "Account", "Currency", "Amount", "Notes"];

    public static string Write(IEnumerable<TransactionDto> transactions)
    {
        var builder = new StringBuilder();
        builder.AppendJoin(',', Header).Append("\r\n");

        foreach (var t in transactions)
        {
            string[] fields =
            [
                t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                t.Type.ToString(),
                Text(t.Description),
                Text(t.CategoryName ?? string.Empty),
                Text(t.AccountName),
                t.Currency,
                (t.Type is Domain.Transactions.TransactionType.Income or Domain.Transactions.TransactionType.TransferIn
                    ? t.Amount
                    : -t.Amount).ToString("0.00", CultureInfo.InvariantCulture),
                Text(t.Notes ?? string.Empty),
            ];

            builder.AppendJoin(',', fields).Append("\r\n");
        }

        return builder.ToString();
    }

    /// <summary>Escapes a free-text field. Values starting with =, +, -, @, tab or CR are
    /// prefixed with an apostrophe so spreadsheet apps do not evaluate them as formulas (OWASP CSV injection).</summary>
    internal static string Text(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        var needsQuotes = value.AsSpan().IndexOfAny(",\"\r\n") >= 0 || value != value.Trim();
        return needsQuotes ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }
}
