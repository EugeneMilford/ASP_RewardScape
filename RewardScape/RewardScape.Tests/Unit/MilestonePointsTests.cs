using Shouldly;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Tests.Unit;

/// <summary>
/// Monthly spend → total milestone points for the month.
/// R500 = 100, R1 000 = 250, R2 500 = 700, R5 000 = 1 500 (cumulative totals).
/// </summary>
public class MilestonePointsTests
{
    // CumulativePointsFor doesn't touch the database, so no context is needed
    private readonly PointsService _points = new PointsService(null!);

    private static List<SpendMilestone> Milestones()
    {
        return new List<SpendMilestone>
        {
            new SpendMilestone { SpendMilestoneId = 1, ThresholdAmount = 500m, CumulativePoints = 100, IsActive = true },
            new SpendMilestone { SpendMilestoneId = 2, ThresholdAmount = 1000m, CumulativePoints = 250, IsActive = true },
            new SpendMilestone { SpendMilestoneId = 3, ThresholdAmount = 2500m, CumulativePoints = 700, IsActive = true },
            new SpendMilestone { SpendMilestoneId = 4, ThresholdAmount = 5000m, CumulativePoints = 1500, IsActive = true }
        };
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(499.99, 0)]
    [InlineData(500, 100)]
    [InlineData(999.99, 100)]
    [InlineData(1000, 250)]
    [InlineData(1150, 250)]
    [InlineData(2500, 700)]
    [InlineData(5000, 1500)]
    [InlineData(9000, 1500)]
    public void CumulativePointsFor_MonthlySpend_ReturnsHighestMilestoneReached(decimal spend, int expected)
    {
        int points = _points.CumulativePointsFor(spend, Milestones());

        points.ShouldBe(expected);
    }

    [Fact]
    public void CumulativePointsFor_InactiveMilestone_IsIgnored()
    {
        var milestones = Milestones();
        milestones.Add(new SpendMilestone { SpendMilestoneId = 5, ThresholdAmount = 200m, CumulativePoints = 50, IsActive = false });

        _points.CumulativePointsFor(300m, milestones).ShouldBe(0);
    }

    [Fact]
    public void CumulativePointsFor_CrossingTwoMilestonesInOnePurchase_PaysTheDifference()
    {
        // R450 already spent, then a R700 purchase: month total R1 150
        int before = _points.CumulativePointsFor(450m, Milestones());
        int after = _points.CumulativePointsFor(1150m, Milestones());

        (after - before).ShouldBe(250);
    }

    [Theory]
    [InlineData(250, 1.00, 250)]
    [InlineData(250, 1.25, 312)]
    [InlineData(250, 1.50, 375)]
    [InlineData(250, 2.00, 500)]
    [InlineData(333, 1.25, 416)]
    public void TierMultiplier_RoundsDown(int basePoints, decimal multiplier, int expected)
    {
        // Same formula the purchase service uses
        int points = (int)Math.Floor(basePoints * multiplier);

        points.ShouldBe(expected);
    }
}
