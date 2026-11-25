using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IDealService
{
    Task<IReadOnlyList<DealDto>> GetDealsAsync(DealQueryDto query);
    Task<IReadOnlyList<MemberDealDto>> GetDealsForMemberAsync(int memberId, bool onlyRedeemable);
    Task<DealDto> GetAsync(int dealId);
    Task<DealDto> CreateAsync(CreateDealDto dto);
    Task<DealDto> UpdateStockAsync(int dealId, UpdateDealStockDto dto);
    decimal CashPriceForTier(Deal deal, Tier tier);
}

public class DealService : IDealService
{
    private readonly SpendWellDbContext _db;
    private readonly IPointsService _points;

    public DealService(SpendWellDbContext db, IPointsService points)
    {
        _db = db;
        _points = points;
    }

    public async Task<IReadOnlyList<DealDto>> GetDealsAsync(DealQueryDto query)
    {
        var deals = BaseQuery();
        var now = DateTime.UtcNow;

        if (query.OnlyAvailable)
        {
            deals = deals.Where(d => d.IsActive && d.StockRemaining > 0 && d.StartsAt <= now && d.EndsAt > now);
        }

        if (!string.IsNullOrWhiteSpace(query.Tier))
        {
            // Deals open to this tier = deals whose minimum tier ranks at or below it
            var tier = await _db.Tiers.FirstOrDefaultAsync(t => t.Name == query.Tier)
                ?? throw NotFoundException.For("Tier", query.Tier);
            deals = deals.Where(d => d.MinTier.Rank <= tier.Rank);
        }

        if (query.MaxPointsCost.HasValue)
        {
            deals = deals.Where(d => d.PointsCost <= query.MaxPointsCost.Value);
        }

        var list = await deals.OrderBy(d => d.PointsCost).ToListAsync();
        return list.Select(d => d.ToDto()).ToList();
    }

    /// <summary>
    /// Every live deal, priced for this member's tier, with a clear reason when they can't redeem it.
    /// onlyRedeemable = true hides the ones they can't redeem right now.
    /// </summary>
    public async Task<IReadOnlyList<MemberDealDto>> GetDealsForMemberAsync(int memberId, bool onlyRedeemable)
    {
        var member = await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);

        var now = DateTime.UtcNow;
        var deals = await BaseQuery()
            .Where(d => d.IsActive && d.StartsAt <= now && d.EndsAt > now)
            .OrderBy(d => d.PointsCost)
            .ToListAsync();
        int availablePoints = await _points.GetAvailablePointsAsync(memberId);

        var usedPerDeal = await _db.Redemptions
            .Where(r => r.MemberId == memberId && r.Status != RedemptionStatus.Cancelled)
            .GroupBy(r => r.DealId)
            .Select(g => new { DealId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DealId, x => x.Count);

        var result = new List<MemberDealDto>();

        foreach (var deal in deals)
        {
            int alreadyRedeemed = usedPerDeal.TryGetValue(deal.DealId, out var count) ? count : 0;
            string? reason = null;

            if (deal.StockRemaining <= 0)
            {
                reason = "Sold out.";
            }
            else if (member.Tier.Rank < deal.MinTier.Rank)
            {
                reason = string.Format("Reach {0} tier to unlock this deal.", deal.MinTier.Name);
            }
            else if (alreadyRedeemed >= deal.MaxPerMember)
            {
                reason = string.Format("Limit of {0} per member reached.", deal.MaxPerMember);
            }
            else if (member.PointsOwed > 0)
            {
                reason = string.Format("You owe {0} points from a refund. Earn more to clear it first.", member.PointsOwed);
            }
            else if (availablePoints < deal.PointsCost)
            {
                reason = string.Format("You need {0} more points.", deal.PointsCost - availablePoints);
            }

            if (onlyRedeemable && reason != null)
            {
                continue;
            }

            result.Add(new MemberDealDto(deal.ToDto(), CashPriceForTier(deal, member.Tier), reason == null, reason));
        }

        return result;
    }

    public async Task<DealDto> GetAsync(int dealId)
    {
        var deal = await BaseQuery().FirstOrDefaultAsync(d => d.DealId == dealId)
            ?? throw NotFoundException.For("Deal", dealId);

        return deal.ToDto();
    }

    public async Task<DealDto> CreateAsync(CreateDealDto dto)
    {
        if (dto.EndsAt <= dto.StartsAt)
        {
            throw new BusinessRuleException("A deal must end after it starts.");
        }

        bool tierExists = await _db.Tiers.AnyAsync(t => t.TierId == dto.MinTierId);
        if (!tierExists)
        {
            throw NotFoundException.For("Tier", dto.MinTierId);
        }

        var deal = new Deal
        {
            Title = dto.Title,
            Description = dto.Description,
            PointsCost = dto.PointsCost,
            CashPrice = dto.CashPrice,
            MinTierId = dto.MinTierId,
            StockRemaining = dto.StockRemaining,
            MaxPerMember = dto.MaxPerMember,
            VoucherValidDays = dto.VoucherValidDays,
            StartsAt = dto.StartsAt,
            EndsAt = dto.EndsAt,
            IsActive = true
        };

        _db.Deals.Add(deal);
        await _db.SaveChangesAsync();

        return await GetAsync(deal.DealId);
    }

    public async Task<DealDto> UpdateStockAsync(int dealId, UpdateDealStockDto dto)
    {
        var deal = await _db.Deals.FirstOrDefaultAsync(d => d.DealId == dealId)
            ?? throw NotFoundException.For("Deal", dealId);

        deal.StockRemaining = dto.StockRemaining;
        deal.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();

        return await GetAsync(deal.DealId);
    }

    /// <summary>Cash part of the deal after the member's tier discount, e.g. Gold gets 10% off.</summary>
    public decimal CashPriceForTier(Deal deal, Tier tier)
    {
        if (deal.CashPrice <= 0)
        {
            return 0m;
        }

        decimal discounted = deal.CashPrice * (1 - tier.DealDiscountPercent / 100m);
        return Math.Round(discounted, 2, MidpointRounding.AwayFromZero);
    }

    private IQueryable<Deal> BaseQuery()
    {
        return _db.Deals
            .Include(d => d.MinTier);
    }
}
