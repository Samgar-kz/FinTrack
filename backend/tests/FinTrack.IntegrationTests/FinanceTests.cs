using System.Net;
using FinTrack.Application.Accounts;
using FinTrack.Application.Admin;
using FinTrack.Application.Budgets;
using FinTrack.Application.Categories;
using FinTrack.Application.Common;
using FinTrack.Application.Reports;
using FinTrack.Application.Transactions;
using FinTrack.Domain.Accounts;
using FinTrack.Domain.Categories;
using FinTrack.Domain.Transactions;
using FinTrack.IntegrationTests.Infrastructure;

namespace FinTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class FinanceTests(FinTrackApiFactory factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Transactions_keep_the_account_balance_in_sync()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var account = await CreateAccount(client, "Chequing", 1_000m);
        var groceries = await Category(client, "Groceries");

        var expense = await (await client.PostJsonAsync("/api/transactions",
            new SaveTransactionRequest(account.Id, TransactionType.Expense, 200m, Today, "Weekly shop", groceries.Id, null))).ReadAsync<TransactionDto>();
        await (await client.PostJsonAsync("/api/transactions",
            new SaveTransactionRequest(account.Id, TransactionType.Income, 50.25m, Today, "Refund", null, null))).ReadAsync<TransactionDto>();

        Assert.Equal(850.25m, (await GetAccount(client, account.Id)).Balance);
        Assert.Equal("Groceries", expense.CategoryName);

        await (await client.PutJsonAsync($"/api/transactions/{expense.Id}",
            new SaveTransactionRequest(account.Id, TransactionType.Expense, 150m, Today, "Weekly shop", groceries.Id, "Edited"))).ReadAsync<TransactionDto>();
        Assert.Equal(900.25m, (await GetAccount(client, account.Id)).Balance);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/{expense.Id}")).StatusCode);
        Assert.Equal(1_050.25m, (await GetAccount(client, account.Id)).Balance);
    }

    [Fact]
    public async Task Users_cannot_access_each_others_data()
    {
        var (alice, _) = await factory.CreateUserClientAsync();
        var (bob, _) = await factory.CreateUserClientAsync();
        var aliceAccount = await CreateAccount(alice, "Alice private", 10m);

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/accounts/{aliceAccount.Id}")).StatusCode);

        var sneaky = await bob.PostJsonAsync("/api/transactions",
            new SaveTransactionRequest(aliceAccount.Id, TransactionType.Expense, 5m, Today, "Not mine", null, null));
        Assert.Equal(HttpStatusCode.NotFound, sneaky.StatusCode);

        var bobsList = await (await bob.GetAsync("/api/accounts")).ReadAsync<List<AccountDto>>();
        Assert.DoesNotContain(bobsList, a => a.Id == aliceAccount.Id);
    }

    [Fact]
    public async Task Transfers_move_money_and_deleting_one_leg_removes_both()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var chequing = await CreateAccount(client, "Chequing", 1_000m);
        var savings = await CreateAccount(client, "Savings", 0m);

        var transfer = await (await client.PostJsonAsync("/api/transfers",
            new CreateTransferRequest(chequing.Id, savings.Id, 400m, Today, "Save"))).ReadAsync<TransferDto>();

        Assert.Equal(600m, (await GetAccount(client, chequing.Id)).Balance);
        Assert.Equal(400m, (await GetAccount(client, savings.Id)).Balance);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/{transfer.Incoming.Id}")).StatusCode);

        Assert.Equal(1_000m, (await GetAccount(client, chequing.Id)).Balance);
        Assert.Equal(0m, (await GetAccount(client, savings.Id)).Balance);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/transactions/{transfer.Outgoing.Id}")).StatusCode);
    }

    [Fact]
    public async Task Transfer_between_currencies_is_rejected_as_a_business_rule()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var cad = await CreateAccount(client, "CAD", 100m);
        var usd = await CreateAccount(client, "USD", 100m, "USD");

        var response = await client.PostJsonAsync("/api/transfers", new CreateTransferRequest(cad.Id, usd.Id, 10m, Today, "FX"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Accounts_with_history_must_be_archived_not_deleted()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var account = await CreateAccount(client, "Old card", 0m);
        (await client.PostJsonAsync("/api/transactions",
            new SaveTransactionRequest(account.Id, TransactionType.Expense, 5m, Today, "Coffee", null, null))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/accounts/{account.Id}")).StatusCode);

        var archived = await (await client.PostAsync($"/api/accounts/{account.Id}/archive", null)).ReadAsync<AccountDto>();
        Assert.True(archived.IsArchived);

        var active = await (await client.GetAsync("/api/accounts")).ReadAsync<List<AccountDto>>();
        Assert.DoesNotContain(active, a => a.Id == account.Id);
    }

    [Fact]
    public async Task Search_filters_sorts_and_paginates()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var account = await CreateAccount(client, "Main", 0m);
        for (var i = 1; i <= 7; i++)
        {
            (await client.PostJsonAsync("/api/transactions",
                new SaveTransactionRequest(account.Id, TransactionType.Expense, i * 10m, Today.AddDays(-i), i % 2 == 0 ? $"Coffee {i}" : $"Lunch {i}", null, null))).EnsureSuccessStatusCode();
        }

        var page = await (await client.GetAsync("/api/transactions?search=coffee&sort=AmountDesc&pageSize=2")).ReadAsync<PagedResult<TransactionDto>>();

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal([60m, 40m], page.Items.Select(t => t.Amount));

        var ranged = await (await client.GetAsync($"/api/transactions?from={Today.AddDays(-3):yyyy-MM-dd}&to={Today:yyyy-MM-dd}")).ReadAsync<PagedResult<TransactionDto>>();
        Assert.Equal(3, ranged.TotalCount);
    }

    [Fact]
    public async Task Export_returns_a_csv_file()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var account = await CreateAccount(client, "Main", 0m);
        (await client.PostJsonAsync("/api/transactions",
            new SaveTransactionRequest(account.Id, TransactionType.Expense, 12.5m, Today, "=cmd|' /C calc'!A0", null, null))).EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/transactions/export");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Date,Type,Description", csv, StringComparison.Ordinal);
        Assert.Contains("'=cmd", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Budgets_track_spending_for_the_month()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var account = await CreateAccount(client, "Card", 0m);
        var dining = await Category(client, "Dining Out");

        var budget = await (await client.PutJsonAsync("/api/budgets",
            new UpsertBudgetRequest(dining.Id, Today.Year, Today.Month, 200m))).ReadAsync<BudgetDto>();
        Assert.Equal(0m, budget.Spent);

        (await client.PostJsonAsync("/api/transactions",
            new SaveTransactionRequest(account.Id, TransactionType.Expense, 150m, new DateOnly(Today.Year, Today.Month, 1), "Dinner", dining.Id, null))).EnsureSuccessStatusCode();

        var budgets = await (await client.GetAsync($"/api/budgets?year={Today.Year}&month={Today.Month}")).ReadAsync<List<BudgetDto>>();
        var tracked = Assert.Single(budgets);
        Assert.Equal(150m, tracked.Spent);
        Assert.Equal(50m, tracked.Remaining);
        Assert.Equal(75m, tracked.PercentUsed);
    }

    [Fact]
    public async Task Budgets_cannot_be_set_for_income_categories()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var salary = (await (await client.GetAsync("/api/categories?kind=Income")).ReadAsync<List<CategoryDto>>()).First(c => c.Name == "Salary");

        var response = await client.PutJsonAsync("/api/budgets", new UpsertBudgetRequest(salary.Id, Today.Year, Today.Month, 100m));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Summary_excludes_transfers_and_other_currencies()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var chequing = await CreateAccount(client, "Chequing", 0m);
        var savings = await CreateAccount(client, "Savings", 0m);
        var usd = await CreateAccount(client, "USD", 0m, "USD");
        var start = new DateOnly(Today.Year, Today.Month, 1);

        await Post(client, chequing.Id, TransactionType.Income, 4_000m, start);
        await Post(client, chequing.Id, TransactionType.Expense, 1_000m, start);
        await Post(client, usd.Id, TransactionType.Expense, 999m, start);
        (await client.PostJsonAsync("/api/transfers", new CreateTransferRequest(chequing.Id, savings.Id, 500m, start, "Save"))).EnsureSuccessStatusCode();

        var summary = await (await client.GetAsync("/api/reports/summary")).ReadAsync<SummaryDto>();

        Assert.Equal("CAD", summary.Currency);
        Assert.Equal(4_000m, summary.Income);
        Assert.Equal(1_000m, summary.Expenses);
        Assert.Equal(3_000m, summary.Net);
        Assert.Equal(75m, summary.SavingsRate);
        Assert.Equal(3_000m, summary.TotalBalance);

        var usdSummary = await (await client.GetAsync("/api/reports/summary?currency=usd")).ReadAsync<SummaryDto>();
        Assert.Equal(999m, usdSummary.Expenses);
    }

    [Fact]
    public async Task Category_breakdown_and_cash_flow_aggregate_by_period()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var account = await CreateAccount(client, "Main", 0m);
        var groceries = await Category(client, "Groceries");
        var start = new DateOnly(Today.Year, Today.Month, 1);

        await Post(client, account.Id, TransactionType.Expense, 300m, start, groceries.Id);
        await Post(client, account.Id, TransactionType.Expense, 100m, start);
        await Post(client, account.Id, TransactionType.Income, 2_000m, start.AddMonths(-1));

        var breakdown = await (await client.GetAsync("/api/reports/categories?kind=Expense")).ReadAsync<List<CategoryBreakdownDto>>();
        Assert.Equal(["Groceries", "Uncategorised"], breakdown.Select(b => b.Name));
        Assert.Equal(75m, breakdown[0].Percentage);

        var cashFlow = await (await client.GetAsync("/api/reports/cash-flow?months=3")).ReadAsync<List<CashFlowPointDto>>();
        Assert.Equal(3, cashFlow.Count);
        Assert.Equal(2_000m, cashFlow[1].Income);
        Assert.Equal(400m, cashFlow[2].Expenses);
    }

    [Fact]
    public async Task Seeded_demo_user_has_realistic_data()
    {
        var client = await factory.LoginAsync(FinTrackApiFactory.DemoEmail, FinTrackApiFactory.DemoPassword);

        var accounts = await (await client.GetAsync("/api/accounts")).ReadAsync<List<AccountDto>>();
        var transactions = await (await client.GetAsync("/api/transactions?pageSize=1")).ReadAsync<PagedResult<TransactionDto>>();

        Assert.Equal(4, accounts.Count);
        Assert.True(transactions.TotalCount > 50);
    }

    [Fact]
    public async Task Admin_endpoints_are_restricted_to_admins()
    {
        var (user, _) = await factory.CreateUserClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/admin/stats")).StatusCode);

        var admin = await factory.LoginAsync(FinTrackApiFactory.AdminEmail, FinTrackApiFactory.AdminPassword);
        var stats = await (await admin.GetAsync("/api/admin/stats")).ReadAsync<PlatformStatsDto>();
        var users = await (await admin.GetAsync("/api/admin/users?search=demo")).ReadAsync<PagedResult<AdminUserDto>>();

        Assert.True(stats.Users >= 2);
        Assert.Contains(users.Items, u => u.Email == FinTrackApiFactory.DemoEmail && u.AccountCount == 4);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private static async Task<AccountDto> CreateAccount(HttpClient client, string name, decimal opening, string currency = "CAD") =>
        await (await client.PostJsonAsync("/api/accounts", new CreateAccountRequest(name, AccountType.Checking, currency, opening))).ReadAsync<AccountDto>();

    private static async Task<AccountDto> GetAccount(HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/accounts/{id}")).ReadAsync<AccountDto>();

    private static async Task<CategoryDto> Category(HttpClient client, string name) =>
        (await (await client.GetAsync("/api/categories?kind=Expense")).ReadAsync<List<CategoryDto>>()).First(c => c.Name == name);

    private static async Task Post(HttpClient client, Guid accountId, TransactionType type, decimal amount, DateOnly date, Guid? categoryId = null) =>
        (await client.PostJsonAsync("/api/transactions",
            new SaveTransactionRequest(accountId, type, amount, date, $"{type} {amount}", categoryId, null))).EnsureSuccessStatusCode();
}
