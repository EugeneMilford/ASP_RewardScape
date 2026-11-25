namespace SpendWell.Api.Models;

/// <summary>
/// Running total of what a member spent in one calendar month, and how many points it earned.
/// One row per member per month.
/// </summary>
public class MonthlySpendTracker
{
    public int MonthlySpendTrackerId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalSpend { get; set; }

    /// <summary>Milestone points before the tier multiplier (matches SpendMilestone.CumulativePoints).</summary>
    public int BasePointsAwarded { get; set; }

    /// <summary>Points actually given to the member after multipliers.</summary>
    public int PointsAwarded { get; set; }

    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
