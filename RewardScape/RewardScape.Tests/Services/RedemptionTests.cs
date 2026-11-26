using Microsoft.EntityFrameworkCore;
using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Tests.TestHelpers;

namespace SpendWell.Tests.Services;

/// <summary>Spending points on deals: FIFO lots, tier locks, limits, cash top-ups, vouchers.</summary>
public class RedemptionTests : DbTestBase
{
    [Fact]
    public async Task Redeem_TakesFromTheSoonestExpiringLotFirst()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var later = await TestData.LotAsync(Db, member, 300, DateTime.UtcNow.AddMonths(11));
        var sooner = await TestData.LotAsync(Db, member, 100, DateTime.UtcNow.AddMonths(1));
        var deal = await TestData.DealAsync(Db, pointsCost: 150);

        var result = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));

        result.LotsUsed.Count.ShouldBe(2);
        result.LotsUsed[0].PointsLotId.ShouldBe(sooner.PointsLotId);
        result.LotsUsed[0].PointsTaken.ShouldBe(100);
        result.LotsUsed[1].PointsLotId.ShouldBe(later.PointsLotId);
        result.LotsUsed[1].PointsTaken.ShouldBe(50);
        result.PointsBalanceAfter.ShouldBe(250);
        result.Redemption.VoucherCode.ShouldStartWith("SW-");

        using var db = Fresh();
        (await db.PointsLots.SingleAsync(l => l.PointsLotId == sooner.PointsLotId)).RemainingPoints.ShouldBe(0);
        (await db.PointsLots.SingleAsync(l => l.PointsLotId == later.PointsLotId)).RemainingPoints.ShouldBe(250);
        (await db.Deals.SingleAsync()).StockRemaining.ShouldBe(9);

        // One ledger line per lot used, so a cancellation can find them
        var redeemLines = await db.PointsTransactions.Where(t => t.Type == PointsTransactionType.Redeem).ToListAsync();
        redeemLines.Count.ShouldBe(2);
        redeemLines.Sum(t => t.Points).ShouldBe(-150);
    }

    [Fact]
    public async Task Cancel_PutsPointsBackIntoTheSameLots_AndReleasesStock()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var later = await TestData.LotAsync(Db, member, 300, DateTime.UtcNow.AddMonths(11));
        var sooner = await TestData.LotAsync(Db, member, 100, DateTime.UtcNow.AddMonths(1));
        var deal = await TestData.DealAsync(Db, pointsCost: 150);
        var redeemed = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));

        var cancelled = await Services.Redemptions.CancelAsync(redeemed.Redemption.Id);

        cancelled.Status.ShouldBe("Cancelled");

        using var db = Fresh();
        (await db.PointsLots.SingleAsync(l => l.PointsLotId == sooner.PointsLotId)).RemainingPoints.ShouldBe(100);
        (await db.PointsLots.SingleAsync(l => l.PointsLotId == later.PointsLotId)).RemainingPoints.ShouldBe(300);
        (await db.Deals.SingleAsync()).StockRemaining.ShouldBe(10);
    }

    [Fact]
    public async Task Redeem_NotEnoughPoints_Throws_AndChangesNothing()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 100, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 150);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null)));

        using var db = Fresh();
        (await db.Deals.SingleAsync()).StockRemaining.ShouldBe(10);
        (await db.Redemptions.CountAsync()).ShouldBe(0);
        (await db.PointsLots.SingleAsync()).RemainingPoints.ShouldBe(100);
    }

    [Fact]
    public async Task Redeem_GoldOnlyDeal_RefusedForBlue()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 1000, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100, minTierId: TestData.GoldTier);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null)));
    }

    [Fact]
    public async Task Redeem_OverThePerMemberLimit_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 1000, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100, maxPerMember: 1);

        await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null)));
    }

    [Fact]
    public async Task Redeem_SoldOut_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 1000, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100, stock: 0);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null)));
    }

    [Fact]
    public async Task Redeem_CashTopUp_GoldPaysTenPercentLess_FromTheAccount()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 1000m, tierId: TestData.GoldTier);
        await TestData.LotAsync(Db, member, 500, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100, cashPrice: 100m);

        var result = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, account.AccountNumber));

        result.Redemption.CashPaid.ShouldBe(90m);
        result.AccountBalanceAfter.ShouldBe(910m);
    }

    [Fact]
    public async Task Redeem_CashDealWithoutAccount_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 500, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100, cashPrice: 50m);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null)));
    }

    [Fact]
    public async Task Cancel_CashDeal_RefundsTheTopUp()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 1000m);
        await TestData.LotAsync(Db, member, 500, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100, cashPrice: 100m);
        var redeemed = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, account.AccountNumber));

        await Services.Redemptions.CancelAsync(redeemed.Redemption.Id);

        using var db = Fresh();
        (await db.Accounts.SingleAsync()).Balance.ShouldBe(1000m);
    }

    [Fact]
    public async Task UseVoucher_WorksOnce()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 500, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100);
        var redeemed = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));
        string code = redeemed.Redemption.VoucherCode;

        var used = await Services.Redemptions.UseVoucherAsync(code, new UseVoucherDto(TestData.VredenburgStore));

        used.Status.ShouldBe("Used");
        used.UsedAt.ShouldNotBeNull();

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Redemptions.UseVoucherAsync(code, new UseVoucherDto(TestData.VredenburgStore)));
    }

    [Fact]
    public async Task UseVoucher_LowercaseCode_StillWorks()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 500, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100);
        var redeemed = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));

        var used = await Services.Redemptions.UseVoucherAsync(
            redeemed.Redemption.VoucherCode.ToLowerInvariant(), new UseVoucherDto(TestData.VredenburgStore));

        used.Status.ShouldBe("Used");
    }

    [Fact]
    public async Task UseVoucher_UnknownStore_ThrowsNotFound()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 500, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100);
        var redeemed = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));

        await Should.ThrowAsync<NotFoundException>(() =>
            Services.Redemptions.UseVoucherAsync(redeemed.Redemption.VoucherCode, new UseVoucherDto(999)));
    }

    [Fact]
    public async Task Cancel_UsedVoucher_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 500, DateTime.UtcNow.AddMonths(6));
        var deal = await TestData.DealAsync(Db, pointsCost: 100);
        var redeemed = await Services.Redemptions.RedeemAsync(deal.DealId, new RedeemDealDto(member.MemberId, null));
        await Services.Redemptions.UseVoucherAsync(redeemed.Redemption.VoucherCode, new UseVoucherDto(TestData.VredenburgStore));

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Redemptions.CancelAsync(redeemed.Redemption.Id));
    }

    [Fact]
    public async Task MemberDeals_ExplainWhyADealCantBeRedeemed()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await TestData.LotAsync(Db, member, 120, DateTime.UtcNow.AddMonths(6));
        await TestData.DealAsync(Db, pointsCost: 100);
        await TestData.DealAsync(Db, pointsCost: 200);
        await TestData.DealAsync(Db, pointsCost: 50, minTierId: TestData.GoldTier);

        var all = await Services.Deals.GetDealsForMemberAsync(member.MemberId, onlyRedeemable: false);
        var redeemable = await Services.Deals.GetDealsForMemberAsync(member.MemberId, onlyRedeemable: true);

        all.Count.ShouldBe(3);
        all.Single(d => d.Deal.PointsCost == 200).ReasonIfNot.ShouldBe("You need 80 more points.");
        all.Single(d => d.Deal.PointsCost == 50).ReasonIfNot!.ShouldContain("Gold");
        redeemable.Single().Deal.PointsCost.ShouldBe(100);
    }
}
