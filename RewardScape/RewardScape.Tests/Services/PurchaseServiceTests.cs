using Microsoft.EntityFrameworkCore;
using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Tests.TestHelpers;

namespace SpendWell.Tests.Services;

/// <summary>Till purchases: money out of the account, monthly milestones, tier multiplier.</summary>
public class PurchaseServiceTests : DbTestBase
{
    [Fact]
    public async Task Purchase_BelowFirstMilestone_DebitsAccount_EarnsNothing()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 10_000m);

        var result = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 450m));

        result.PointsEarned.ShouldBe(0);
        result.AccountBalanceAfter.ShouldBe(9550m);
        result.MonthlySpendSoFar.ShouldBe(450m);
        result.AmountToNextMilestone.ShouldBe(50m);
    }

    [Fact]
    public async Task Purchase_CrossingTwoMilestones_Earns250()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);

        await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 450m));
        var second = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 700m));

        second.PointsEarned.ShouldBe(250);
        second.MonthlySpendSoFar.ShouldBe(1150m);
        second.AmountToNextMilestone.ShouldBe(1350m);

        using var db = Fresh();
        (await TestData.SpendablePointsAsync(db, member.MemberId)).ShouldBe(250);
    }

    [Fact]
    public async Task Purchase_GoldMember_GetsOneAndAHalfTimes()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, tierId: TestData.GoldTier);

        var result = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1000m));

        result.PointsEarned.ShouldBe(375);
    }

    [Fact]
    public async Task Purchase_SameSlipTwice_ThrowsConflict_AndChargesOnce()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 1000m);

        await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 100m, "SLIP-DUP"));

        await Should.ThrowAsync<ConflictException>(() =>
            Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 100m, "SLIP-DUP")));

        using var db = Fresh();
        (await db.Purchases.CountAsync()).ShouldBe(1);
        (await db.Accounts.SingleAsync()).Balance.ShouldBe(900m);
    }

    [Fact]
    public async Task Purchase_InsufficientFunds_Throws_AndSavesNothing()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 100m);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 450m)));

        using var db = Fresh();
        (await db.Purchases.CountAsync()).ShouldBe(0);
        (await db.Accounts.SingleAsync()).Balance.ShouldBe(100m);
    }

    [Fact]
    public async Task Purchase_SomeoneElsesAccount_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var (_, otherAccount) = await TestData.MemberWithAccountAsync(Db);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Purchases.CreateAsync(TestData.TillPurchase(member, otherAccount, 50m)));
    }

    [Fact]
    public async Task Purchase_ThatQualifiesForSilver_UpgradesStraightAway()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        await Services.Points.AwardAsync(member, 600, 1m, RewardCategory.Bonus, "Test", 0, "Earlier points");
        await Db.SaveChangesAsync();

        // R5 000 = 1 500 points → 2 100 in 12 months, Silver needs 2 000
        var result = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 5000m));

        result.PointsEarned.ShouldBe(1500);
        result.TierChangedTo.ShouldBe("Silver");

        using var db = Fresh();
        (await db.Members.SingleAsync()).TierId.ShouldBe(TestData.SilverTier);
        (await db.TierHistories.CountAsync()).ShouldBe(1);
    }
}

/// <summary>Refunds: money back, and milestone points clawed back if the month drops below one.</summary>
public class RefundTests : DbTestBase
{
    [Fact]
    public async Task Refund_DroppingBelowMilestones_ClawsBackThePoints()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 10_000m);
        await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 450m));
        var big = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 700m));

        var refund = await Services.Purchases.RefundAsync(big.Purchase.Id, new CreateRefundDto(700m, "Returned"));

        refund.PointsReversed.ShouldBe(250);
        refund.PointsOwed.ShouldBe(0);
        refund.AccountBalanceAfter.ShouldBe(9550m);
        refund.PurchaseStatus.ShouldBe("Refunded");

        using var db = Fresh();
        (await TestData.SpendablePointsAsync(db, member.MemberId)).ShouldBe(0);
    }

    [Fact]
    public async Task Refund_StillAboveTheMilestone_ClawsBackNothing()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var purchase = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1200m));

        var refund = await Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(100m, "One item"));

        refund.PointsReversed.ShouldBe(0);
        refund.PurchaseStatus.ShouldBe("PartiallyRefunded");
    }

    [Fact]
    public async Task Refund_DroppingOneMilestone_ClawsBackOnlyThatMilestone()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var purchase = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1200m));

        // R1 200 → R900: from the R1 000 milestone (250) back to R500 (100)
        var refund = await Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(300m, "Two items"));

        refund.PointsReversed.ShouldBe(150);
    }

    [Fact]
    public async Task Refund_GoldMember_ClawsBackAtTheMultiplierPaid()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, tierId: TestData.GoldTier);
        var purchase = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1000m));

        var refund = await Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(1000m, "Returned"));

        refund.PointsReversed.ShouldBe(375);
    }

    [Fact]
    public async Task Refund_PointsAlreadySpent_BecomePointsOwed_AndBlockRedeeming()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var purchase = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1000m)); // 250 points
        var deal = await TestData.DealAsync(Db, pointsCost: 200);
        await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));               // 50 left

        var refund = await Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(1000m, "Returned"));

        refund.PointsReversed.ShouldBe(250);
        refund.PointsOwed.ShouldBe(200);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null)));
    }

    [Fact]
    public async Task NewEarnings_PayOffPointsOwedFirst()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var purchase = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1000m));
        var deal = await TestData.DealAsync(Db, pointsCost: 200);
        await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));
        await Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(1000m, "Returned")); // owes 200

        // Month spend is back to R0; R600 crosses R500 again = 100 points, all used to pay the debt
        await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 600m));

        using var db = Fresh();
        (await db.Members.SingleAsync()).PointsOwed.ShouldBe(100);
        (await TestData.SpendablePointsAsync(db, member.MemberId)).ShouldBe(-100);
    }

    [Fact]
    public async Task Refund_MoreThanIsLeft_Throws()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var purchase = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1200m));
        await Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(100m, "First"));

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(1200m, "Too much")));
    }
}
