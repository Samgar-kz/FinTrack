using System.Text.RegularExpressions;
using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;

namespace FinTrack.Domain.Categories;

public enum CategoryKind
{
    Expense = 0,
    Income = 1,
}

public sealed partial class Category : Entity
{
    public const int NameMaxLength = 50;

    private Category()
    {
        // Required by EF Core.
    }

    public Category(Guid userId, string name, CategoryKind kind, string color)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Unknown category kind.");
        }

        UserId = userId;
        Kind = kind;
        Update(name, color);
    }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Whether the category classifies income or expenses. Immutable once created.</summary>
    public CategoryKind Kind { get; private set; }

    /// <summary>Hex colour (#RRGGBB) used by charts in the UI.</summary>
    public string Color { get; private set; } = "#64748B";

    public static bool IsValidColor(string? color) => color is not null && HexColor().IsMatch(color);

    /// <summary>Starter categories created for every new user.</summary>
    public static IReadOnlyList<Category> CreateDefaults(Guid userId) =>
    [
        new(userId, "Housing", CategoryKind.Expense, "#6366F1"),
        new(userId, "Groceries", CategoryKind.Expense, "#22C55E"),
        new(userId, "Dining Out", CategoryKind.Expense, "#F97316"),
        new(userId, "Transportation", CategoryKind.Expense, "#0EA5E9"),
        new(userId, "Utilities", CategoryKind.Expense, "#EAB308"),
        new(userId, "Healthcare", CategoryKind.Expense, "#EF4444"),
        new(userId, "Entertainment", CategoryKind.Expense, "#A855F7"),
        new(userId, "Shopping", CategoryKind.Expense, "#EC4899"),
        new(userId, "Travel", CategoryKind.Expense, "#14B8A6"),
        new(userId, "Education", CategoryKind.Expense, "#8B5CF6"),
        new(userId, "Subscriptions", CategoryKind.Expense, "#F43F5E"),
        new(userId, "Other", CategoryKind.Expense, "#64748B"),
        new(userId, "Salary", CategoryKind.Income, "#16A34A"),
        new(userId, "Freelance", CategoryKind.Income, "#0284C7"),
        new(userId, "Investments", CategoryKind.Income, "#7C3AED"),
        new(userId, "Other Income", CategoryKind.Income, "#475569"),
    ];

    public void Update(string name, string color)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > NameMaxLength)
        {
            throw new DomainException($"Category name is required and must be at most {NameMaxLength} characters.");
        }

        if (!IsValidColor(color))
        {
            throw new DomainException("Colour must be a hex value such as #22C55E.");
        }

        Name = name.Trim();
        Color = color.ToUpperInvariant();
    }

    /// <summary>True when a transaction of the given type may be classified with this category.</summary>
    public bool Accepts(TransactionType type) => type switch
    {
        TransactionType.Income => Kind == CategoryKind.Income,
        TransactionType.Expense => Kind == CategoryKind.Expense,
        _ => false,
    };

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
