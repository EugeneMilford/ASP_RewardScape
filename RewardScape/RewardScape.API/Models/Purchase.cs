namespace SpendWell.Api.Models;

public class Purchase
{
    public int PurchaseId { get; set; }

    /// <summary>The till slip number. Unique, so the same slip can never be posted twice.</summary>
    public string ExternalReference { get; set; } = string.Empty;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;

    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Completed;

    /// <summary>Points this purchase earned (after tier multiplier). Shown on the spending summary.</summary>
    public int PointsEarned { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
