namespace SpendWell.Api.Models;

/// <summary>
/// Append-only points ledger. Rows are never updated or deleted: every change to a member's
/// points is a new row, so the history can always be rebuilt and audited.
/// </summary>
public class PointsTransaction
{
    public int PointsTransactionId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public PointsTransactionType Type { get; set; }

    /// <summary>
    /// Shopping, Travel, Driving or Bonus for points earned or clawed back.
    /// Null for movements that aren't earnings (redeem, restore, expire).
    /// </summary>
    public RewardCategory? Category { get; set; }

    /// <summary>Positive for points in, negative for points out.</summary>
    public int Points { get; set; }

    public decimal MultiplierApplied { get; set; } = 1.0m;

    /// <summary>What caused this entry, e.g. "Purchase", "TravelBooking", "DrivingMonthlySummary".</summary>
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }

    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
