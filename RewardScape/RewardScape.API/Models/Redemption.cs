namespace SpendWell.Api.Models;

public class Redemption
{
    public int RedemptionId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int DealId { get; set; }
    public Deal Deal { get; set; } = null!;

    /// <summary>Account the cash part was paid from (null if the deal had no cash price).</summary>
    public int? AccountId { get; set; }
    public Account? Account { get; set; }

    public int PointsSpent { get; set; }
    public decimal CashPaid { get; set; }

    public string VoucherCode { get; set; } = string.Empty;
    public RedemptionStatus Status { get; set; } = RedemptionStatus.Issued;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
