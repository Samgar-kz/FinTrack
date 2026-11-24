using FinTrack.Domain.Common;
using FinTrack.Domain.Transactions;
using static FinTrack.UnitTests.Domain.TestData;

namespace FinTrack.UnitTests.Domain;

public sealed class TransactionTests
{
    [Fact]
    public void Recording_income_increases_the_account_balance()
    {
        var account = Account(opening: 100m);

        var transaction = Transaction.Record(account, TransactionType.Income, 250.50m, Today, "Salary", Income());

        Assert.Equal(350.50m, account.Balance);
        Assert.Equal(250.50m, transaction.SignedAmount);
        Assert.Equal(account.Id, transaction.AccountId);
        Assert.Equal(account.UserId, transaction.UserId);
    }

    [Fact]
    public void Recording_an_expense_decreases_the_account_balance()
    {
        var account = Account(opening: 100m);

        var transaction = Transaction.Record(account, TransactionType.Expense, 40m, Today, "Groceries", Expense());

        Assert.Equal(60m, account.Balance);
        Assert.Equal(-40m, transaction.SignedAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.005)]
    public void Invalid_amounts_are_rejected(decimal amount)
    {
        var account = Account();

        Assert.Throws<DomainException>(() =>
            Transaction.Record(account, TransactionType.Expense, amount, Today, "Coffee", null));
        Assert.Equal(1_000m, account.Balance);
    }

    [Fact]
    public void Description_is_required()
    {
        Assert.Throws<DomainException>(() =>
            Transaction.Record(Account(), TransactionType.Expense, 5m, Today, "   ", null));
    }

    [Theory]
    [InlineData(TransactionType.TransferIn)]
    [InlineData(TransactionType.TransferOut)]
    public void Transfer_types_cannot_be_recorded_directly(TransactionType type)
    {
        Assert.Throws<DomainException>(() => Transaction.Record(Account(), type, 5m, Today, "Move", null));
    }

    [Fact]
    public void Category_kind_must_match_transaction_type()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Transaction.Record(Account(), TransactionType.Expense, 5m, Today, "Lunch", Income()));

        Assert.Contains("cannot be used for expense", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_of_another_user_is_rejected()
    {
        var foreignCategory = Expense(userId: Guid.CreateVersion7());

        Assert.Throws<DomainException>(() =>
            Transaction.Record(Account(), TransactionType.Expense, 5m, Today, "Lunch", foreignCategory));
    }

    [Fact]
    public void Archived_accounts_do_not_accept_postings()
    {
        var account = Account();
        account.Archive();

        Assert.Throws<DomainException>(() =>
            Transaction.Record(account, TransactionType.Income, 5m, Today, "Refund", null));
    }

    [Fact]
    public void Amending_moves_the_effect_between_accounts()
    {
        var original = Account(opening: 500m);
        var target = Account(opening: 500m);
        var transaction = Transaction.Record(original, TransactionType.Expense, 100m, Today, "Dinner", Expense());

        transaction.Amend(original, target, TransactionType.Expense, 80m, Today, "Dinner (split)", Expense(), "Shared");

        Assert.Equal(500m, original.Balance);
        Assert.Equal(420m, target.Balance);
        Assert.Equal(target.Id, transaction.AccountId);
        Assert.Equal("Shared", transaction.Notes);
    }

    [Fact]
    public void Amending_can_switch_between_income_and_expense()
    {
        var account = Account(opening: 0m);
        var transaction = Transaction.Record(account, TransactionType.Expense, 30m, Today, "Refund?", null);

        transaction.Amend(account, account, TransactionType.Income, 30m, Today, "Refund", null, null);

        Assert.Equal(30m, account.Balance);
    }

    [Fact]
    public void Reversing_restores_the_balance()
    {
        var account = Account(opening: 1_000m);
        var transaction = Transaction.Record(account, TransactionType.Expense, 123.45m, Today, "Phone bill", null);

        transaction.Reverse(account);

        Assert.Equal(1_000m, account.Balance);
    }

    [Fact]
    public void Transfer_moves_money_and_links_both_legs()
    {
        var from = Account(opening: 1_000m);
        var to = Account(opening: 0m);

        var (outgoing, incoming) = Transaction.CreateTransfer(from, to, 250m, Today, "Savings");

        Assert.Equal(750m, from.Balance);
        Assert.Equal(250m, to.Balance);
        Assert.NotNull(outgoing.TransferId);
        Assert.Equal(outgoing.TransferId, incoming.TransferId);
        Assert.Equal(TransactionType.TransferOut, outgoing.Type);
        Assert.Equal(TransactionType.TransferIn, incoming.Type);
    }

    [Fact]
    public void Transfer_to_the_same_account_is_rejected()
    {
        var account = Account();

        Assert.Throws<DomainException>(() => Transaction.CreateTransfer(account, account, 10m, Today, "Loop"));
    }

    [Fact]
    public void Transfer_between_currencies_is_rejected()
    {
        var cad = Account(currency: "CAD");
        var usd = Account(currency: "USD");

        var ex = Assert.Throws<DomainException>(() => Transaction.CreateTransfer(cad, usd, 10m, Today, "FX"));

        Assert.Contains("different currencies", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1_000m, cad.Balance);
        Assert.Equal(1_000m, usd.Balance);
    }

    [Fact]
    public void Transfer_to_another_users_account_is_rejected()
    {
        var mine = Account();
        var theirs = Account(userId: Guid.CreateVersion7());

        Assert.Throws<DomainException>(() => Transaction.CreateTransfer(mine, theirs, 10m, Today, "Gift"));
    }

    [Fact]
    public void Transfers_cannot_be_amended()
    {
        var from = Account();
        var to = Account();
        var (outgoing, _) = Transaction.CreateTransfer(from, to, 10m, Today, "Move");

        Assert.Throws<DomainException>(() =>
            outgoing.Amend(from, from, TransactionType.Expense, 10m, Today, "Changed", null, null));
    }
}
