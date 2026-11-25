using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IMemberService
{
    Task<MemberDto> CreateAsync(CreateMemberDto dto);
    Task<MemberDto> GetAsync(int memberId);
    Task<PointsBalanceDto> GetPointsBalanceAsync(int memberId);
    Task<MonthlyProgressDto> GetMonthlyProgressAsync(int memberId, int? year, int? month);
    Task<PagedResult<PointsTransactionDto>> GetPointsLedgerAsync(int memberId, int page, int pageSize);
    Task<IReadOnlyList<PointsLotDto>> GetPointsLotsAsync(int memberId, bool includeEmpty);
    Task<IReadOnlyList<TierHistoryDto>> GetTierHistoryAsync(int memberId);
    Task<RewardsSummaryDto> GetRewardsSummaryAsync(int memberId, int? year);
}

public class MemberService : IMemberService
{
    private const int BlueTierId = 1;

    private readonly SpendWellDbContext _db;
    private readonly IAccountService _accounts;
    private readonly IPointsService _points;

    public MemberService(SpendWellDbContext db, IAccountService accounts, IPointsService points)
    {
        _db = db;
        _accounts = accounts;
        _points = points;
    }

    /// <summary>Signs up a member on the Blue tier and opens their first cheque account.</summary>
    public async Task<MemberDto> CreateAsync(CreateMemberDto dto)
    {
        string email = dto.Email.Trim().ToLowerInvariant();

        bool emailTaken = await _db.Members.AnyAsync(m => m.Email == email);
        if (emailTaken)
        {
            throw new ConflictException(string.Format("A member with email {0} already exists.", email));
        }

        var blue = await _db.Tiers.FirstAsync(t => t.TierId == BlueTierId);

        var member = new Member
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PhoneNumber = dto.PhoneNumber.Trim(),
            JoinedAt = DateTime.UtcNow,
            Tier = blue
        };

        _db.Members.Add(member);
        await _accounts.CreateAccountEntityAsync(member, AccountType.Cheque, dto.OpeningDeposit);
        await _db.SaveChangesAsync();

        return member.ToDto();
    }

    public async Task<MemberDto> GetAsync(int memberId)
    {
        var member = await _db.Members
            .Include(m => m.Tier)
            .Include(m => m.Accounts)
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);

        return member.ToDto();
    }

    public async Task<PointsBalanceDto> GetPointsBalanceAsync(int memberId)
    {
        var member = await FindMemberAsync(memberId);
        var now = DateTime.UtcNow;
        var in30Days = now.AddDays(30);

        var liveLots = await _db.PointsLots
            .Where(l => l.MemberId == memberId && !l.IsExpired && l.RemainingPoints > 0 && l.ExpiresAt > now)
            .OrderBy(l => l.ExpiresAt)
            .ToListAsync();

        int inLots = liveLots.Sum(l => l.RemainingPoints);
        int expiringSoon = liveLots.Where(l => l.ExpiresAt <= in30Days).Sum(l => l.RemainingPoints);
        DateTime? nextExpiry = liveLots.Count > 0 ? liveLots[0].ExpiresAt : null;

        return new PointsBalanceDto(
            memberId,
            inLots,
            member.PointsOwed,
            Math.Max(inLots - member.PointsOwed, 0),
            expiringSoon,
            nextExpiry);
    }

    public async Task<MonthlyProgressDto> GetMonthlyProgressAsync(int memberId, int? year, int? month)
    {
        var member = await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);

        var now = DateTime.UtcNow;
        int y = year ?? now.Year;
        int mth = month ?? now.Month;

        if (mth < 1 || mth > 12)
        {
            throw new BusinessRuleException("Month must be between 1 and 12.");
        }

        // Worked out from the month's purchases: spend net of refunds, points net of claw-backs
        var monthStart = new DateTime(y, mth, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var purchases = _db.Purchases
            .Where(p => p.MemberId == memberId && p.PurchasedAt >= monthStart && p.PurchasedAt < monthEnd);

        var amounts = await purchases.Select(p => new { p.Amount, p.RefundedAmount }).ToListAsync();
        decimal spend = amounts.Sum(a => a.Amount - a.RefundedAmount);

        int pointsEarned = await purchases.SumAsync(p => (int?)p.PointsEarned) ?? 0;
        int pointsReversed = await _db.Refunds
            .Where(r => purchases.Select(p => p.PurchaseId).Contains(r.PurchaseId))
            .SumAsync(r => (int?)r.PointsReversed) ?? 0;
        int earned = pointsEarned - pointsReversed;

        var milestones = await _points.GetActiveMilestonesAsync();
        var next = milestones.FirstOrDefault(m => m.ThresholdAmount > spend);

        return new MonthlyProgressDto(
            memberId,
            y,
            mth,
            spend,
            earned,
            member.Tier.Name,
            member.Tier.EarnMultiplier,
            next != null ? next.ThresholdAmount : null,
            next != null ? next.ThresholdAmount - spend : null,
            next != null ? next.CumulativePoints : null,
            milestones.Select(m => new MilestoneProgressDto(m.ThresholdAmount, m.CumulativePoints, spend >= m.ThresholdAmount)).ToList());
    }

    public async Task<PagedResult<PointsTransactionDto>> GetPointsLedgerAsync(int memberId, int page, int pageSize)
    {
        await FindMemberAsync(memberId);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.PointsTransactions.Where(t => t.MemberId == memberId);
        int total = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.PointsTransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<PointsTransactionDto>(items.Select(t => t.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<PointsLotDto>> GetPointsLotsAsync(int memberId, bool includeEmpty)
    {
        await FindMemberAsync(memberId);

        var query = _db.PointsLots.Where(l => l.MemberId == memberId);
        if (!includeEmpty)
        {
            query = query.Where(l => l.RemainingPoints > 0 && !l.IsExpired);
        }

        var lots = await query.OrderBy(l => l.ExpiresAt).ToListAsync();
        return lots.Select(l => l.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<TierHistoryDto>> GetTierHistoryAsync(int memberId)
    {
        await FindMemberAsync(memberId);

        return await _db.TierHistories
            .Where(h => h.MemberId == memberId)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new TierHistoryDto(h.FromTier.Name, h.ToTier.Name, h.ChangedAt, h.Reason))
            .ToListAsync();
    }

    /// <summary>
    /// Points earned per category (shopping, travel, driving, bonus) this month and this year,
    /// net of refund claw-backs, and the distance to the next tier.
    /// </summary>
    public async Task<RewardsSummaryDto> GetRewardsSummaryAsync(int memberId, int? year)
    {
        var member = await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);

        var now = DateTime.UtcNow;
        int y = year ?? now.Year;
        var yearStart = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = yearStart.AddYears(1);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var entries = await _db.PointsTransactions
            .Where(t => t.MemberId == memberId
                && t.Category != null
                && t.CreatedAt >= yearStart
                && t.CreatedAt < yearEnd
                && (t.Type == PointsTransactionType.Earn
                    || t.Type == PointsTransactionType.Reverse
                    || t.Type == PointsTransactionType.Bonus))
            .Select(t => new { Category = t.Category!.Value, t.Points, t.CreatedAt })
            .ToListAsync();

        var byCategory = new List<CategoryPointsDto>();
        foreach (var category in Enum.GetValues<RewardCategory>())
        {
            int thisYear = entries.Where(e => e.Category == category).Sum(e => e.Points);
            int thisMonth = entries.Where(e => e.Category == category && e.CreatedAt >= monthStart).Sum(e => e.Points);
            byCategory.Add(new CategoryPointsDto(category.ToString(), thisMonth, thisYear));
        }

        int rolling = await _points.GetPointsEarnedRolling12MonthsAsync(memberId);
        int spendable = await _points.GetAvailablePointsAsync(memberId);

        var nextTier = await _db.Tiers
            .Where(t => t.Rank > member.Tier.Rank)
            .OrderBy(t => t.Rank)
            .FirstOrDefaultAsync();

        return new RewardsSummaryDto(
            memberId,
            member.Tier.Name,
            member.Tier.EarnMultiplier,
            member.Tier.TravelDiscountPercent,
            y,
            Math.Max(spendable, 0),
            byCategory.Sum(c => c.PointsThisYear),
            byCategory,
            rolling,
            nextTier != null ? nextTier.Name : null,
            nextTier != null ? (int?)Math.Max(nextTier.MinPointsRolling12M - rolling, 0) : null);
    }

    private async Task<Member> FindMemberAsync(int memberId)
    {
        return await _db.Members.FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);
    }
}
