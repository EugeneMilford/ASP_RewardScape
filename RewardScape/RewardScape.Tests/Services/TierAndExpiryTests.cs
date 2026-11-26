using Microsoft.EntityFrameworkCore;
using Shouldly;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Tests.TestHelpers;

namespace SpendWell.Tests.Services;

public class TierTests : DbTestBase
{
    [Fact]
    public async Task Upgrade_At2000PointsIn12Months_MovesToSilver()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await Services.Points.AwardAsync(member, 2000, 1m, RewardCategory.Bonus, "Test", 0, "Seed");
        await Db.SaveChangesAsync();

        var newTier = await Services.Tiers.UpgradeIfQualifiedAsync(member);
        await Db.SaveChangesAsync();

        newTier.ShouldNotBeNull();
        newTier.Name.ShouldBe("Silver");

        using var db = Fresh();
        var history = await db.TierHistories.SingleAsync();
        history.FromTierId.ShouldBe(TestData.BlueTier);
        history.ToTierId.ShouldBe(TestData.SilverTier);
    }

    [Fact]
    public async Task Upgrade_NotEnoughPoints_StaysPut()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await Services.Points.AwardAsync(member, 1999, 1m, RewardCategory.Bonus, "Test", 0, "Seed");
        await Db.SaveChangesAsync();

        var newTier = await Services.Tiers.UpgradeIfQualifiedAsync(member);

        newTier.ShouldBeNull();
    }

    [Fact]
    public async Task Upgrade_NeverDowngrades_OutsideTheAnnualReview()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db, tierId: TestData.GoldTier);

        var newTier = await Services.Tiers.UpgradeIfQualifiedAsync(member);

        newTier.ShouldBeNull();
        member.TierId.ShouldBe(TestData.GoldTier);
    }

    [Fact]
    public async Task AnnualReview_DowngradesAMemberWhoEarnedTooLittle()
    {
        await TestData.MemberWithAccountAsync(Db, tierId: TestData.GoldTier);

        var result = await Services.Tiers.RunAnnualReviewAsync();

        result.Downgraded.ShouldBe(1);

        using var db = Fresh();
        (await db.Members.SingleAsync()).TierId.ShouldBe(TestData.BlueTier);
    }

    [Fact]
    public async Task SpendingPoints_DoesNotCountAgainstTierProgress()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await Services.Points.AwardAsync(member, 2500, 1m, RewardCategory.Bonus, "Test", 0, "Seed");
        await Db.SaveChangesAsync();
        var deal = await TestData.DealAsync(Db, pointsCost: 2000);

        await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));

        (await Services.Points.GetPointsEarnedRolling12MonthsAsync(member.MemberId)).ShouldBe(2500);
    }
}

public class ExpiryTests : DbTestBase
{
    [Fact]
    public async Task Run_ExpiresOldLots_AndWritesALedgerLine()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var oldLot = await TestData.LotAsync(Db, member, 80, DateTime.UtcNow.AddDays(-1));
        var liveLot = await TestData.LotAsync(Db, member, 50, DateTime.UtcNow.AddMonths(3));

        var result = await Services.Expiry.RunAsync();

        result.LotsExpired.ShouldBe(1);
        result.PointsExpired.ShouldBe(80);

        using var db = Fresh();
        var expired = await db.PointsLots.SingleAsync(l => l.PointsLotId == oldLot.PointsLotId);
        expired.IsExpired.ShouldBeTrue();
        expired.RemainingPoints.ShouldBe(0);
        (await db.PointsLots.SingleAsync(l => l.PointsLotId == liveLot.PointsLotId)).RemainingPoints.ShouldBe(50);

        var line = await db.PointsTransactions.SingleAsync(t => t.Type == PointsTransactionType.Expire);
        line.Points.ShouldBe(-80);
    }

    [Fact]
    public async Task Run_ExpiresUnusedVouchersPastTheirDate()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var deal = await TestData.DealAsync(Db);

        Db.Redemptions.Add(new Redemption
        {
            MemberId = member.MemberId,
            DealId = deal.DealId,
            PointsSpent = 150,
            VoucherCode = "SW-TEST-0001",
            Status = RedemptionStatus.Issued,
            IssuedAt = DateTime.UtcNow.AddDays(-40),
            ExpiresAt = DateTime.UtcNow.AddDays(-10)
        });
        await Db.SaveChangesAsync();

        var result = await Services.Expiry.RunAsync();

        result.VouchersExpired.ShouldBe(1);

        using var db = Fresh();
        (await db.Redemptions.SingleAsync()).Status.ShouldBe(RedemptionStatus.Expired);
    }

    [Fact]
    public async Task Run_Twice_DoesNothingTheSecondTime()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 80, DateTime.UtcNow.AddDays(-1));

        await Services.Expiry.RunAsync();
        var second = await Services.Expiry.RunAsync();

        second.LotsExpired.ShouldBe(0);
        second.PointsExpired.ShouldBe(0);
    }
}

public class RewardsSummaryTests : DbTestBase
{
    [Fact]
    public async Task Summary_SplitsPointsByCategory_AndShowsDistanceToNextTier()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await Services.Points.AwardAsync(member, 100, 1m, RewardCategory.Shopping, "Test", 0, "Shop");
        await Services.Points.AwardAsync(member, 300, 1m, RewardCategory.Travel, "Test", 0, "Trip");
        await Services.Points.AwardAsync(member, 200, 1m, RewardCategory.Driving, "Test", 0, "Drive");
        await Db.SaveChangesAsync();

        var summary = await Services.Members.GetRewardsSummaryAsync(member.MemberId, null);

        summary.ByCategory.Single(c => c.Category == "Shopping").PointsThisYear.ShouldBe(100);
        summary.ByCategory.Single(c => c.Category == "Travel").PointsThisYear.ShouldBe(300);
        summary.ByCategory.Single(c => c.Category == "Driving").PointsThisMonth.ShouldBe(200);
        summary.ByCategory.Single(c => c.Category == "Bonus").PointsThisYear.ShouldBe(0);
        summary.TotalEarnedThisYear.ShouldBe(600);
        summary.SpendablePoints.ShouldBe(600);
        summary.NextTier.ShouldBe("Silver");
        summary.PointsToNextTier.ShouldBe(1400);
    }

    [Fact]
    public async Task Summary_RefundClawback_ReducesShopping()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var purchase = await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1000m));
        await Services.Purchases.RefundAsync(purchase.Purchase.Id, new CreateRefundDto(1000m, "Returned"));

        var summary = await Services.Members.GetRewardsSummaryAsync(member.MemberId, null);

        summary.ByCategory.Single(c => c.Category == "Shopping").PointsThisYear.ShouldBe(0);
    }

    [Fact]
    public async Task MonthlyProgress_ShowsSpendAndGapToNextMilestone()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        await Services.Purchases.CreateAsync(TestData.TillPurchase(member, account, 1150m));

        var progress = await Services.Members.GetMonthlyProgressAsync(member.MemberId, null, null);

        progress.TotalSpend.ShouldBe(1150m);
        progress.PointsEarnedThisMonth.ShouldBe(250);
        progress.NextMilestoneThreshold.ShouldBe(2500m);
        progress.AmountToNextMilestone.ShouldBe(1350m);
        progress.Milestones.Count(m => m.Reached).ShouldBe(2);
    }
}
