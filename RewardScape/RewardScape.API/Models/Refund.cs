namespace SpendWell.Api.Models;

public class Refund
{
    public int RefundId { get; set; }

    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Points clawed back because of this refund.</summary>
    public int PointsReversed { get; set; }
}
