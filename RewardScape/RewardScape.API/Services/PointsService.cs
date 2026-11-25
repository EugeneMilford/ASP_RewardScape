using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IPointsService
{
    int CumulativePointsFor(decimal monthlySpend, IReadOnlyList<SpendMilestone> milestones);
    Task<IReadOnlyList<SpendMilestone>> GetActiveMilestonesAsync();
    Task<int> AwardAsync(Member member, int points, decimal multiplier, RewardCategory category, string sourceType, int sourceId, string description);
    Task<int> ClawBackAsync(Member member, int points, RewardCategory category, string sourceType, int sourceId, string description);
    Task<List<LotUsageDto>> ConsumeAsync(Member member, int points, Redemption redemption);
    Task RestoreAsync(Redemption redemption);
    Task<int> GetAvailablePointsAsync(int memberId);
    Task<int> GetPointsEarnedRolling12MonthsAsync(int memberId);
}

/// <summary>
/// All points movement lives here. Methods change tracked entities and add ledger rows
/// but never call SaveChanges, so the caller controls the database transaction.
/// </summary>
public class PointsService : IPointsService
{
    private const int LotLifetimeMonths = 12;
    private const int RestoredLotGraceDays = 30;

    private readonly SpendWellDbContext _db;

    public PointsService(SpendWellDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Total milestone points for a month's spend. R1 150 with milestones
    /// R500 = 100 and R1 000 = 250 returns 250.
    /// </summary>
    public int CumulativePointsFor(decimal monthlySpend, IReadOnlyList<SpendMilestone> milestones)
    {
        int best = 0;
        foreach (var milestone in milestones)
        {
            if (milestone.IsActive && monthlySpend >= milestone.ThresholdAmount && milestone.CumulativePoints > best)
            {
                best = milestone.CumulativePoints;
            }
        }
        return best;
    }

    public async Task<IReadOnlyList<SpendMilestone>> GetActiveMilestonesAsync()
    {
        // Sorted in memory: there are only a handful, and some databases (SQLite in the
        // tests) can't ORDER BY a decimal column.
        var milestones = await _db.SpendMilestones
            .Where(m => m.IsActive)
            .ToListAsync();

        return milestones.OrderBy(m => m.ThresholdAmount).ToList();
    }

    /// <summary>
    /// Gives a member points. Any points they still owe from an earlier refund are paid off first;
    /// whatever is left becomes a new lot that expires in 12 months.
    /// Returns the number of points that landed in a spendable lot.
    /// </summary>
    public Task<int> AwardAsync(Member member, int points, decimal multiplier, RewardCategory category, string sourceType, int sourceId, string description)
    {
        if (points <= 0)
        {
            return Task.FromResult(0);
        }

        var now = DateTime.UtcNow;

        _db.PointsTransactions.Add(new PointsTransaction
        {
            MemberId = member.MemberId,
            Type = PointsTransactionType.Earn,
            Category = category,
            Points = points,
            MultiplierApplied = multiplier,
            SourceType = sourceType,
            SourceId = sourceId,
            Description = description,
            CreatedAt = now
        });

        int remaining = points;
        if (member.PointsOwed > 0)
        {
            int payOff = Math.Min(member.PointsOwed, remaining);
            member.PointsOwed -= payOff;
            remaining -= payOff;
        }

        if (remaining > 0)
        {
            _db.PointsLots.Add(new PointsLot
            {
                MemberId = member.MemberId,
                OriginalPoints = remaining,
                RemainingPoints = remaining,
                EarnedAt = now,
                ExpiresAt = now.AddMonths(LotLifetimeMonths)
            });
        }

        return Task.FromResult(remaining);
    }

    /// <summary>
    /// Takes points back after a refund. Takes from the newest lots first (those are the
    /// ones the refunded spending created). If the member already spent them, the shortfall
    /// is recorded as PointsOwed and blocks redemptions until new earnings cover it.
    /// Returns how many points could not be taken from lots (added to PointsOwed).
    /// </summary>
    public async Task<int> ClawBackAsync(Member member, int points, RewardCategory category, string sourceType, int sourceId, string description)
    {
        if (points <= 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;

        var lots = await _db.PointsLots
            .Where(l => l.MemberId == member.MemberId && !l.IsExpired && l.RemainingPoints > 0 && l.ExpiresAt > now)
            .OrderByDescending(l => l.EarnedAt)
            .ToListAsync();

        int stillToTake = points;
        foreach (var lot in lots)
        {
            if (stillToTake == 0)
            {
                break;
            }

            int take = Math.Min(lot.RemainingPoints, stillToTake);
            lot.RemainingPoints -= take;
            stillToTake -= take;
        }

        if (stillToTake > 0)
        {
            member.PointsOwed += stillToTake;
        }

        _db.PointsTransactions.Add(new PointsTransaction
        {
            MemberId = member.MemberId,
            Type = PointsTransactionType.Reverse,
            Category = category,
            Points = -points,
            SourceType = sourceType,
            SourceId = sourceId,
            Description = description,
            CreatedAt = now
        });

        return stillToTake;
    }

    /// <summary>
    /// Spends points on a redemption, oldest-expiring lots first (FIFO).
    /// The ledger gets one Redeem line PER LOT used (SourceType "PointsLot", SourceId = lot Id),
    /// so the ledger itself records which lots paid and a cancellation can put the points back.
    /// </summary>
    public async Task<List<LotUsageDto>> ConsumeAsync(Member member, int points, Redemption redemption)
    {
        var now = DateTime.UtcNow;

        var lots = await _db.PointsLots
            .Where(l => l.MemberId == member.MemberId && !l.IsExpired && l.RemainingPoints > 0 && l.ExpiresAt > now)
            .OrderBy(l => l.ExpiresAt)
            .ThenBy(l => l.PointsLotId)
            .ToListAsync();

        int available = lots.Sum(l => l.RemainingPoints) - member.PointsOwed;
        if (available < points)
        {
            throw new BusinessRuleException(string.Format(
                "Not enough points. You have {0}, this deal needs {1}.", Math.Max(available, 0), points));
        }

        var usage = new List<LotUsageDto>();
        int stillNeeded = points;

        foreach (var lot in lots)
        {
            if (stillNeeded == 0)
            {
                break;
            }

            int take = Math.Min(lot.RemainingPoints, stillNeeded);
            lot.RemainingPoints -= take;
            stillNeeded -= take;

            _db.PointsTransactions.Add(new PointsTransaction
            {
                MemberId = member.MemberId,
                Type = PointsTransactionType.Redeem,
                Points = -take,
                SourceType = LotSourceType,
                SourceId = lot.PointsLotId,
                Description = RedeemDescription(redemption.VoucherCode),
                CreatedAt = now
            });

            usage.Add(new LotUsageDto(lot.PointsLotId, take, lot.ExpiresAt));
        }

        return usage;
    }

    /// <summary>
    /// Puts a cancelled redemption's points back into the lots they came from (found through
    /// the per-lot Redeem lines in the ledger), keeping those lots' expiry dates. If a lot has
    /// already expired, its points go into a new lot with a 30-day grace period instead of vanishing.
    /// </summary>
    public async Task RestoreAsync(Redemption redemption)
    {
        var now = DateTime.UtcNow;
        string redeemDescription = RedeemDescription(redemption.VoucherCode);

        var redeemLines = await _db.PointsTransactions
            .Where(t => t.MemberId == redemption.MemberId
                && t.Type == PointsTransactionType.Redeem
                && t.SourceType == LotSourceType
                && t.Description == redeemDescription)
            .ToListAsync();

        var lotIds = redeemLines.Where(t => t.SourceId.HasValue).Select(t => t.SourceId!.Value).Distinct().ToList();
        var lots = await _db.PointsLots.Where(l => lotIds.Contains(l.PointsLotId)).ToListAsync();

        foreach (var line in redeemLines)
        {
            int pointsBack = -line.Points;
            var lot = lots.FirstOrDefault(l => l.PointsLotId == line.SourceId);

            if (lot == null || lot.IsExpired || lot.ExpiresAt <= now)
            {
                lot = new PointsLot
                {
                    MemberId = redemption.MemberId,
                    OriginalPoints = pointsBack,
                    RemainingPoints = pointsBack,
                    EarnedAt = now,
                    ExpiresAt = now.AddDays(RestoredLotGraceDays)
                };
                _db.PointsLots.Add(lot);
            }
            else
            {
                lot.RemainingPoints += pointsBack;
            }

            _db.PointsTransactions.Add(new PointsTransaction
            {
                MemberId = redemption.MemberId,
                Type = PointsTransactionType.Restore,
                Points = pointsBack,
                SourceType = LotSourceType,
                SourceId = line.SourceId,
                Description = string.Format("Cancelled voucher {0}", redemption.VoucherCode),
                CreatedAt = now
            });
        }
    }

    private const string LotSourceType = "PointsLot";

    /// <summary>Voucher codes are unique, so this text identifies one redemption's ledger lines.</summary>
    private static string RedeemDescription(string voucherCode)
    {
        return string.Format("Redeemed voucher {0}", voucherCode);
    }

    /// <summary>Spendable points: unexpired lot points minus anything the member owes.</summary>
    public async Task<int> GetAvailablePointsAsync(int memberId)
    {
        var now = DateTime.UtcNow;

        int inLots = await _db.PointsLots
            .Where(l => l.MemberId == memberId && !l.IsExpired && l.ExpiresAt > now)
            .SumAsync(l => (int?)l.RemainingPoints) ?? 0;

        int owed = await _db.Members
            .Where(m => m.MemberId == memberId)
            .Select(m => m.PointsOwed)
            .FirstOrDefaultAsync();

        return inLots - owed;
    }

    /// <summary>
    /// Points earned (minus refund reversals) in the last 12 months. Spending and expiry
    /// don't count against this, so redeeming a deal never drops your tier.
    /// </summary>
    public async Task<int> GetPointsEarnedRolling12MonthsAsync(int memberId)
    {
        var since = DateTime.UtcNow.AddMonths(-12);

        return await _db.PointsTransactions
            .Where(t => t.MemberId == memberId
                && t.CreatedAt >= since
                && (t.Type == PointsTransactionType.Earn
                    || t.Type == PointsTransactionType.Bonus
                    || t.Type == PointsTransactionType.Reverse))
            .SumAsync(t => (int?)t.Points) ?? 0;
    }
}
