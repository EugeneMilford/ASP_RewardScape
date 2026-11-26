using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Tests.Unit;

public class AccountPostingTests
{
    private static Account NewAccount(decimal balance, AccountStatus status = AccountStatus.Active)
    {
        return new Account { AccountNumber = "6200000001", Balance = balance, Status = status };
    }

    [Fact]
    public void Debit_LowersBalance_AndWritesStatementLine()
    {
        var account = NewAccount(500m);

        var line = AccountPosting.Debit(account, 120m, AccountTransactionType.PurchaseDebit, "SLIP-1", "Groceries");

        account.Balance.ShouldBe(380m);
        line.Direction.ShouldBe(TransactionDirection.Debit);
        line.Amount.ShouldBe(120m);
        line.BalanceAfter.ShouldBe(380m);
        account.Transactions.ShouldContain(line);
    }

    [Fact]
    public void Debit_ExactBalance_IsAllowed()
    {
        var account = NewAccount(100m);

        AccountPosting.Debit(account, 100m, AccountTransactionType.PurchaseDebit, "SLIP-2", "All of it");

        account.Balance.ShouldBe(0m);
    }

    [Fact]
    public void Debit_MoreThanBalance_Throws_AndChangesNothing()
    {
        var account = NewAccount(100m);

        Should.Throw<BusinessRuleException>(() =>
            AccountPosting.Debit(account, 100.01m, AccountTransactionType.PurchaseDebit, "SLIP-3", "Too much"));

        account.Balance.ShouldBe(100m);
        account.Transactions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Debit_ZeroOrNegative_Throws(decimal amount)
    {
        var account = NewAccount(100m);

        Should.Throw<BusinessRuleException>(() =>
            AccountPosting.Debit(account, amount, AccountTransactionType.PurchaseDebit, "SLIP-4", "Bad"));
    }

    [Theory]
    [InlineData(AccountStatus.Frozen)]
    [InlineData(AccountStatus.Closed)]
    public void Debit_FrozenOrClosedAccount_Throws(AccountStatus status)
    {
        var account = NewAccount(1000m, status);

        Should.Throw<BusinessRuleException>(() =>
            AccountPosting.Debit(account, 10m, AccountTransactionType.PurchaseDebit, "SLIP-5", "Blocked"));
    }

    [Fact]
    public void Credit_RaisesBalance_AndWritesStatementLine()
    {
        var account = NewAccount(50m);

        var line = AccountPosting.Credit(account, 25m, AccountTransactionType.RefundCredit, "RF-1", "Refund");

        account.Balance.ShouldBe(75m);
        line.Direction.ShouldBe(TransactionDirection.Credit);
        line.BalanceAfter.ShouldBe(75m);
    }

    [Fact]
    public void Credit_FrozenAccount_IsAllowed_SoRefundsStillLand()
    {
        var account = NewAccount(50m, AccountStatus.Frozen);

        AccountPosting.Credit(account, 25m, AccountTransactionType.RefundCredit, "RF-2", "Refund");

        account.Balance.ShouldBe(75m);
    }

    [Fact]
    public void Credit_ClosedAccount_Throws()
    {
        var account = NewAccount(50m, AccountStatus.Closed);

        Should.Throw<BusinessRuleException>(() =>
            AccountPosting.Credit(account, 25m, AccountTransactionType.RefundCredit, "RF-3", "Refund"));
    }
}
