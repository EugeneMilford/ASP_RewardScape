using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

/// <summary>What RecordPurchaseAsync hands back to the till endpoint or to online checkout.</summary>
public record PurchaseRecordResult(
    Purchase Purchase,
    int PointsAwarded,
    decimal MonthlySpend,
    decimal? AmountToNextMilestone,
    string? TierChangedTo);

public interface IPurchaseService
{
    Task<PurchaseResultDto> CreateAsync(CreatePurchaseDto dto);
    Task<PurchaseDto> GetAsync(int purchaseId);
    Task<PagedResult<PurchaseDto>> GetForMemberAsync(int memberId, int page, int pageSize);
    Task<RefundResultDto> RefundAsync(int purchaseId, CreateRefundDto dto);

    // Building blocks used by other services (online checkout and order cancellation)
    Task<PurchaseRecordResult> RecordPurchaseAsync(Member member, Account account, Store store, List<PurchaseItem> items, string reference, string description);
    Task<RefundResultDto> RefundPurchaseAsync(Purchase purchase, decimal amount, string reason);
}

/// <summary>
/// Shopping spend, whether at a till or online, ends up here: the account is debited,
/// the month's spend goes up, and any milestone crossed pays out Shopping points.
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly SpendWellDbContext _db;
    private readonly IPointsService _points;
    private readonly ITierService _tiers;

    public PurchaseService(SpendWellDbContext db, IPointsService points, ITierService tiers)
    {
        _db = db;
        _points = points;
        _tiers = tiers;
    }

    /// <summary>Till purchase. Validates the request, then records it.</summary>
    public async Task<PurchaseResultDto> CreateAsync(CreatePurchaseDto dto)
    {
        // Idempotency: the same till slip can only ever be charged once
        var existing = await _db.Purchases
            .Where(p => p.ExternalReference == dto.ExternalReference)
            .Select(p => (int?)p.PurchaseId)
            .FirstOrDefaultAsync();

        if (existing.HasValue)
        {
            throw new ConflictException(string.Format(
                "Slip {0} has already been processed as purchase {1}.", dto.ExternalReference, existing.Value));
        }

        var member = await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == dto.MemberId)
            ?? throw NotFoundException.For("Member", dto.MemberId);

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == dto.AccountNumber)
            ?? throw NotFoundException.For("Account", dto.AccountNumber);

        if (account.MemberId != member.MemberId)
        {
            throw new BusinessRuleException("This account does not belong to the member.");
        }

        var store = await _db.Stores.FirstOrDefaultAsync(s => s.StoreId == dto.StoreId && s.IsActive)
            ?? throw NotFoundException.For("Store", dto.StoreId);

        var items = dto.Items.Select(i => new PurchaseItem
        {
            ProductName = i.ProductName,
            Category = i.Category,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineTotal = Math.Round(i.Quantity * i.UnitPrice, 2)
        }).ToList();

        var result = await RecordPurchaseAsync(member, account, store, items, dto.ExternalReference,
            string.Format("Purchase at {0}", store.Name));

        return new PurchaseResultDto(
            result.Purchase.ToDto(),
            account.Balance,
            result.PointsAwarded,
            result.MonthlySpend,
            result.AmountToNextMilestone,
            result.TierChangedTo);
    }

    /// <summary>
    /// Debits the account and awards milestone points in one transaction.
    /// Member must be loaded with Tier. Joins the caller's transaction if there is one.
    /// </summary>
    public async Task<PurchaseRecordResult> RecordPurchaseAsync(
        Member member, Account account, Store store, List<PurchaseItem> items, string reference, string description)
    {
        if (items.Count == 0)
        {
            throw new BusinessRuleException("A purchase needs at least one item.");
        }

        decimal total = items.Sum(i => i.LineTotal);
        var now = DateTime.UtcNow;

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        // 1. Record the purchase and take the money out of the account
        var purchase = new Purchase
        {
            ExternalReference = reference,
            Member = member,
            Account = account,
            Store = store,
            Amount = total,
            PurchasedAt = now,
            Status = PurchaseStatus.Completed,
            Items = items
        };
        _db.Purchases.Add(purchase);

        var debit = AccountPosting.Debit(account, total, AccountTransactionType.PurchaseDebit, reference, description);
        debit.Purchase = purchase;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException
            && ex.InnerException != null
            && ex.InnerException.Message.Contains("ExternalReference"))
        {
            // Two tills raced with the same slip and the unique index caught it
            throw new ConflictException(string.Format("Reference {0} has already been processed.", reference));
        }

        // 2. Work out the month's spend from the purchases themselves (this one is saved, so it's
        //    included) and award any milestones this purchase crossed.
        //    Account.RowVersion stops two purchases on the same account both crossing the same milestone.
        decimal monthSpend = await GetMonthlySpendAsync(member.MemberId, now.Year, now.Month);
        decimal spendBefore = monthSpend - total;

        var milestones = await _points.GetActiveMilestonesAsync();
        int baseToAward = _points.CumulativePointsFor(monthSpend, milestones)
            - _points.CumulativePointsFor(spendBefore, milestones);

        int pointsAwarded = 0;
        if (baseToAward > 0)
        {
            decimal multiplier = member.Tier.EarnMultiplier;
            pointsAwarded = (int)Math.Floor(baseToAward * multiplier);

            await _points.AwardAsync(member, pointsAwarded, multiplier, RewardCategory.Shopping, "Purchase", purchase.MemberId,
                string.Format("Reached R{0:N0} spend for {1:MMMM yyyy}", monthSpend, now));

            purchase.PointsEarned = pointsAwarded;
        }

        await _db.SaveChangesAsync();

        // 3. Tier check needs the new points saved first
        string? tierChangedTo = null;
        if (pointsAwarded > 0)
        {
            var newTier = await _tiers.UpgradeIfQualifiedAsync(member);
            if (newTier != null)
            {
                tierChangedTo = newTier.Name;
                await _db.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();

        var next = milestones.FirstOrDefault(m => m.ThresholdAmount > monthSpend);

        return new PurchaseRecordResult(
            purchase,
            pointsAwarded,
            monthSpend,
            next != null ? next.ThresholdAmount - monthSpend : null,
            tierChangedTo);
    }

    public async Task<PurchaseDto> GetAsync(int purchaseId)
    {
        var purchase = await LoadPurchaseQuery()
            .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId)
            ?? throw NotFoundException.For("Purchase", purchaseId);

        return purchase.ToDto();
    }

    public async Task<PagedResult<PurchaseDto>> GetForMemberAsync(int memberId, int page, int pageSize)
    {
        bool exists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!exists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = LoadPurchaseQuery().Where(p => p.MemberId == memberId);
        int total = await query.CountAsync();

        var purchases = await query
            .OrderByDescending(p => p.PurchasedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<PurchaseDto>(purchases.Select(p => p.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<RefundResultDto> RefundAsync(int purchaseId, CreateRefundDto dto)
    {
        var purchase = await _db.Purchases
            .Include(p => p.Account)
            .Include(p => p.Member)
            .FirstOrDefaultAsync(p => p.PurchaseId == purchaseId)
            ?? throw NotFoundException.For("Purchase", purchaseId);

        return await RefundPurchaseAsync(purchase, dto.Amount, dto.Reason);
    }

    /// <summary>
    /// Puts money back in the account and lowers that month's spend. If the month drops
    /// below a milestone, the milestone points are clawed back at the multiplier they were earned at.
    /// Purchase must be loaded with Account and Member.
    /// </summary>
    public async Task<RefundResultDto> RefundPurchaseAsync(Purchase purchase, decimal amount, string reason)
    {
        decimal refundable = purchase.Amount - purchase.RefundedAmount;
        if (amount <= 0)
        {
            throw new BusinessRuleException("Refund amount must be greater than zero.");
        }
        if (amount > refundable)
        {
            throw new BusinessRuleException(string.Format(
                "Only R{0:N2} of this purchase can still be refunded.", refundable));
        }

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        // 1. Refund record and money back into the account
        var refund = new Refund
        {
            Purchase = purchase,
            Amount = amount,
            Reason = reason,
            RefundedAt = DateTime.UtcNow
        };
        _db.Refunds.Add(refund);

        purchase.RefundedAmount += amount;
        if (purchase.RefundedAmount >= purchase.Amount)
        {
            purchase.Status = PurchaseStatus.Refunded;
        }
        else
        {
            purchase.Status = PurchaseStatus.PartiallyRefunded;
        }

        var credit = AccountPosting.Credit(purchase.Account, amount, AccountTransactionType.RefundCredit,
            string.Format("RF-{0}", purchase.ExternalReference),
            string.Format("Refund: {0}", reason));
        credit.Purchase = purchase;
        credit.Refund = refund;

        await _db.SaveChangesAsync();

        // 2. The refund lowers the spend of the month the purchase was made in (not the current
        //    month). If that month drops below a milestone, take those milestone points back.
        int year = purchase.PurchasedAt.Year;
        int month = purchase.PurchasedAt.Month;

        decimal spendAfter = await GetMonthlySpendAsync(purchase.MemberId, year, month);
        decimal spendBefore = spendAfter + amount;

        var milestones = await _points.GetActiveMilestonesAsync();
        int baseLost = _points.CumulativePointsFor(spendBefore, milestones)
            - _points.CumulativePointsFor(spendAfter, milestones);

        int pointsReversed = 0;

        if (baseLost > 0)
        {
            var monthPoints = await GetMonthShoppingPointsAsync(purchase.MemberId, year, month);

            // Claw back at the multiplier the month was actually paid at, and never more than it paid
            pointsReversed = (int)Math.Round(baseLost * monthPoints.AverageMultiplier, MidpointRounding.AwayFromZero);
            pointsReversed = Math.Min(pointsReversed, Math.Max(monthPoints.NetPoints, 0));

            if (pointsReversed > 0)
            {
                await _points.ClawBackAsync(purchase.Member, pointsReversed, RewardCategory.Shopping, "Refund", refund.RefundId,
                    string.Format("Refund on {0} dropped {1:MMMM yyyy} below a milestone",
                        purchase.ExternalReference, purchase.PurchasedAt));

                refund.PointsReversed = pointsReversed;
            }
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new RefundResultDto(
            refund.RefundId,
            purchase.PurchaseId,
            amount,
            purchase.Account.Balance,
            pointsReversed,
            purchase.Member.PointsOwed,
            purchase.Status.ToString());
    }

    /// <summary>
    /// A member's net shopping spend for one calendar month: every purchase made that month
    /// (till and online) minus what has been refunded on them. Includes saved changes only.
    /// </summary>
    private async Task<decimal> GetMonthlySpendAsync(int memberId, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // Summed in memory: a member's purchases for one month are few, and this avoids
        // decimal arithmetic in SQL that SQLite (used by the tests) can't do
        var amounts = await _db.Purchases
            .Where(p => p.MemberId == memberId && p.PurchasedAt >= monthStart && p.PurchasedAt < monthEnd)
            .Select(p => new { p.Amount, p.RefundedAmount })
            .ToListAsync();

        return amounts.Sum(a => a.Amount - a.RefundedAmount);
    }

    private record MonthPoints(int NetPoints, decimal AverageMultiplier);

    /// <summary>
    /// Shopping points a month's purchases have earned, less what refunds already took back,
    /// and the average tier multiplier they were paid at (from the ledger's Earn lines).
    /// </summary>
    private async Task<MonthPoints> GetMonthShoppingPointsAsync(int memberId, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var purchases = _db.Purchases
            .Where(p => p.MemberId == memberId && p.PurchasedAt >= monthStart && p.PurchasedAt < monthEnd);

        int earned = await purchases.SumAsync(p => (int?)p.PointsEarned) ?? 0;

        int alreadyReversed = await _db.Refunds
            .Where(r => purchases.Select(p => p.PurchaseId).Contains(r.PurchaseId))
            .SumAsync(r => (int?)r.PointsReversed) ?? 0;

        var purchaseIds = await purchases.Select(p => p.PurchaseId).ToListAsync();

        var earnLines = await _db.PointsTransactions
            .Where(t => t.MemberId == memberId
                && t.Type == PointsTransactionType.Earn
                && t.SourceType == "Purchase"
                && t.SourceId.HasValue
                && purchaseIds.Contains(t.SourceId.Value))
            .Select(t => new { t.Points, t.MultiplierApplied })
            .ToListAsync();

        decimal paidPoints = earnLines.Sum(l => (decimal)l.Points);
        decimal basePoints = earnLines.Sum(l => l.MultiplierApplied == 0 ? l.Points : l.Points / l.MultiplierApplied);
        decimal averageMultiplier = basePoints == 0 ? 1m : paidPoints / basePoints;

        return new MonthPoints(earned - alreadyReversed, averageMultiplier);
    }

    private IQueryable<Purchase> LoadPurchaseQuery()
    {
        return _db.Purchases
            .Include(p => p.Account)
            .Include(p => p.Store)
            .Include(p => p.Items);
    }
}
