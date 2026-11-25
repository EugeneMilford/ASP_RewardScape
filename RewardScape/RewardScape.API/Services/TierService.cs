using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface ITierService
{
    Task<IReadOnlyList<TierDto>> GetTiersAsync();
    Task<Tier?> UpgradeIfQualifiedAsync(Member member);
    Task<TierReviewResultDto> RunAnnualReviewAsync();
}

/// <summary>
/// Upgrades happen the moment a member qualifies. Downgrades only happen at the
/// annual review, so a quiet month never costs anyone their tier.
/// </summary>
public class TierService : ITierService
{
    private readonly SpendWellDbContext _db;
    private readonly IPointsService _points;

    public TierService(SpendWellDbContext db, IPointsService points)
    {
        _db = db;
        _points = points;
    }

    public async Task<IReadOnlyList<TierDto>> GetTiersAsync()
    {
        return await _db.Tiers
            .OrderBy(t => t.Rank)
            .Select(t => t.ToDto())
            .ToListAsync();
    }

    /// <summary>
    /// Call after the points have been saved. Returns the new tier if the member moved up,
    /// otherwise null. Does not call SaveChanges.
    /// </summary>
    public async Task<Tier?> UpgradeIfQualifiedAsync(Member member)
    {
        int earned = await _points.GetPointsEarnedRolling12MonthsAsync(member.MemberId);
        var qualifiedTier = await GetTierForPointsAsync(earned);

        var currentTier = await _db.Tiers.FirstAsync(t => t.TierId == member.TierId);
        if (qualifiedTier.Rank <= currentTier.Rank)
        {
            return null;
        }

        ChangeTier(member, currentTier, qualifiedTier,
            string.Format("Upgraded: earned {0} points in the last 12 months.", earned));
        return qualifiedTier;
    }

    public async Task<TierReviewResultDto> RunAnnualReviewAsync()
    {
        var tiers = await _db.Tiers.OrderBy(t => t.Rank).ToListAsync();
        var members = await _db.Members.ToListAsync();

        int upgraded = 0;
        int downgraded = 0;
        int unchanged = 0;

        foreach (var member in members)
        {
            int earned = await _points.GetPointsEarnedRolling12MonthsAsync(member.MemberId);
            var qualified = PickTier(tiers, earned);
            var current = tiers.First(t => t.TierId == member.TierId);

            if (qualified.Rank > current.Rank)
            {
                ChangeTier(member, current, qualified,
                    string.Format("Annual review: earned {0} points, upgraded.", earned));
                upgraded++;
            }
            else if (qualified.Rank < current.Rank)
            {
                ChangeTier(member, current, qualified,
                    string.Format("Annual review: earned {0} points, below {1} requirement of {2}.",
                        earned, current.Name, current.MinPointsRolling12M));
                downgraded++;
            }
            else
            {
                unchanged++;
            }
        }

        await _db.SaveChangesAsync();

        return new TierReviewResultDto(members.Count, upgraded, downgraded, unchanged);
    }

    private async Task<Tier> GetTierForPointsAsync(int earned)
    {
        var tiers = await _db.Tiers.OrderBy(t => t.Rank).ToListAsync();
        return PickTier(tiers, earned);
    }

    private static Tier PickTier(IReadOnlyList<Tier> tiersByRank, int earned)
    {
        Tier result = tiersByRank[0];
        foreach (var tier in tiersByRank)
        {
            if (earned >= tier.MinPointsRolling12M)
            {
                result = tier;
            }
        }
        return result;
    }

    private void ChangeTier(Member member, Tier from, Tier to, string reason)
    {
        member.TierId = to.TierId;
        member.Tier = to;

        _db.TierHistories.Add(new TierHistory
        {
            MemberId = member.MemberId,
            FromTierId = from.TierId,
            ToTierId = to.TierId,
            ChangedAt = DateTime.UtcNow,
            Reason = reason
        });
    }
}
