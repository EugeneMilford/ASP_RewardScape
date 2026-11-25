namespace SpendWell.Api.Models;

/// <summary>
/// A batch of points earned at one moment. Each lot expires 12 months after it was earned.
/// Redemptions take points from the lots that expire soonest first (FIFO).
/// </summary>
public class PointsLot
{
    public int PointsLotId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int OriginalPoints { get; set; }
    public int RemainingPoints { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public bool IsExpired { get; set; }
}
