using Microsoft.EntityFrameworkCore;
using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Tests.TestHelpers;

namespace SpendWell.Tests.Services;

public class MemberServiceTests : DbTestBase
{
    [Fact]
    public async Task Create_StartsOnBlue_WithAnAccountAndOpeningDeposit()
    {
        var member = await Services.Members.CreateAsync(
            new CreateMemberDto("Test Member", "  Test.Member@Example.com ", "0821234567", 2500m));

        member.Tier.ShouldBe("Blue");
        member.Email.ShouldBe("test.member@example.com");
        member.Accounts.Count.ShouldBe(1);
        member.Accounts[0].AccountNumber.Length.ShouldBe(10);
        member.Accounts[0].AccountNumber.ShouldStartWith("62");
        member.Accounts[0].Balance.ShouldBe(2500m);

        using var db = Fresh();
        var line = await db.AccountTransactions.SingleAsync();
        line.Type.ShouldBe(AccountTransactionType.Deposit);
        line.BalanceAfter.ShouldBe(2500m);
    }

    [Fact]
    public async Task Create_NoOpeningDeposit_WritesNoStatementLine()
    {
        await Services.Members.CreateAsync(new CreateMemberDto("Test Member", "zero@example.com", "0821234567", 0m));

        using var db = Fresh();
        (await db.AccountTransactions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ThrowsConflict()
    {
        await Services.Members.CreateAsync(new CreateMemberDto("First", "same@example.com", "0821234567", 0m));

        await Should.ThrowAsync<ConflictException>(() =>
            Services.Members.CreateAsync(new CreateMemberDto("Second", "SAME@example.com", "0821234567", 0m)));
    }

    [Fact]
    public async Task Get_UnknownMember_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => Services.Members.GetAsync(999));
    }
}

public class AccountServiceTests : DbTestBase
{
    [Fact]
    public async Task Deposit_RaisesBalance()
    {
        var (_, account) = await TestData.MemberWithAccountAsync(Db, balance: 100m);

        var line = await Services.Accounts.DepositAsync(account.AccountNumber, new DepositDto(400m, "Salary"));

        line.BalanceAfter.ShouldBe(500m);
        line.Direction.ShouldBe("Credit");
    }

    [Fact]
    public async Task Deposit_FrozenAccount_Throws()
    {
        var (_, account) = await TestData.MemberWithAccountAsync(Db);
        account.Status = AccountStatus.Frozen;
        await Db.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Accounts.DepositAsync(account.AccountNumber, new DepositDto(50m, null)));
    }

    [Fact]
    public async Task SpendingSummary_ShowsMoneyOutPerPurchase_AndRefunds()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 5000m);

        var first = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 300m));
        await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 200m));
        await Services.Purchases.RefundAsync(first.Purchase.Id, new CreateRefundDto(50m, "Damaged item"));

        var now = DateTime.UtcNow;
        var summary = await Services.Accounts.GetSpendingSummaryAsync(account.AccountNumber, now.AddDays(-1), now.AddDays(1));

        summary.TotalSpentOnPurchases.ShouldBe(500m);
        summary.TotalRefunded.ShouldBe(50m);
        summary.NetShoppingSpend.ShouldBe(450m);
        summary.TotalMoneyOut.ShouldBe(500m);
        summary.TotalMoneyIn.ShouldBe(50m);
        summary.ClosingBalance.ShouldBe(4550m);
        summary.Purchases.Count.ShouldBe(2);
        summary.ByStore.Single().StoreName.ShouldBe("SpendWell Vredenburg");
        summary.ByCategory.Single().Category.ShouldBe("Groceries");
    }

    [Fact]
    public async Task SpendingSummary_ToBeforeFrom_Throws()
    {
        var (_, account) = await TestData.MemberWithAccountAsync(Db);
        var now = DateTime.UtcNow;

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Accounts.GetSpendingSummaryAsync(account.AccountNumber, now, now.AddDays(-1)));
    }
}
