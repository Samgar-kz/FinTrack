namespace FinTrack.Domain.Common;

/// <summary>Currency and amount rules shared by the domain and the API validators.</summary>
public static class Money
{
    /// <summary>Largest absolute amount accepted for a single posting (fits numeric(18,2)).</summary>
    public const decimal MaxAmount = 9_999_999_999_999.99m;

    /// <summary>ISO 4217 codes the application supports.</summary>
    public static readonly IReadOnlySet<string> SupportedCurrencies =
        new HashSet<string>(StringComparer.Ordinal) { "CAD", "USD", "EUR", "GBP", "KZT" };

    public static bool IsSupportedCurrency(string? code) =>
        code is not null && SupportedCurrencies.Contains(code);

    /// <summary>True when the amount is positive, within range and has at most two decimal places.</summary>
    public static bool IsValidPositiveAmount(decimal amount) =>
        amount > 0 && amount <= MaxAmount && decimal.Round(amount, 2) == amount;

    public static string NormalizeCurrency(string code)
    {
        var normalized = code.Trim().ToUpperInvariant();
        if (!IsSupportedCurrency(normalized))
        {
            throw new DomainException($"Currency '{code}' is not supported.");
        }

        return normalized;
    }
}
