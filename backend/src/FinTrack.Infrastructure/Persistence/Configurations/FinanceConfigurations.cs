using FinTrack.Domain.Accounts;
using FinTrack.Domain.Budgets;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinTrack.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(Account.NameMaxLength).IsRequired();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(a => a.OpeningBalance).HasPrecision(18, 2);
        builder.Property(a => a.Balance).HasPrecision(18, 2);

        // Npgsql maps a uint row version onto PostgreSQL's xmin system column,
        // giving optimistic concurrency without an extra column.
        builder.Property(a => a.Version).IsRowVersion();

        builder.HasIndex(a => new { a.UserId, a.Name });
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Color).HasMaxLength(7).IsFixedLength();
        builder.HasIndex(c => new { c.UserId, c.Kind, c.Name }).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Description).HasMaxLength(Transaction.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(Transaction.NotesMaxLength);
        builder.ToTable(t => t.HasCheckConstraint("ck_transactions_amount_positive", "amount > 0"));

        builder.Ignore(t => t.IsTransfer);
        builder.Ignore(t => t.SignedAmount);

        builder.HasOne(t => t.Account)
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(t => t.Category)
            .WithMany()
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.UserId, t.Date });
        builder.HasIndex(t => new { t.AccountId, t.Date });
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => t.TransferId);
    }
}

internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(b => b.Limit).HasPrecision(18, 2);
        builder.Ignore(b => b.PeriodStart);
        builder.Ignore(b => b.PeriodEnd);
        builder.ToTable(t => t.HasCheckConstraint("ck_budgets_month", "month BETWEEN 1 AND 12"));

        builder.HasOne(b => b.Category)
            .WithMany()
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>().WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(b => new { b.UserId, b.CategoryId, b.Year, b.Month }).IsUnique();
    }
}
