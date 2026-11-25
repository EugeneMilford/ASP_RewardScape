namespace SpendWell.Api.Models;

public class Deal
{
    public int DealId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int PointsCost { get; set; }

    /// <summary>Optional cash top-up paid from the member's account (before tier discount).</summary>
    public decimal CashPrice { get; set; }

    /// <summary>Lowest tier allowed to redeem this deal.</summary>
    public int MinTierId { get; set; }
    public Tier MinTier { get; set; } = null!;

    public int StockRemaining { get; set; }
    public int MaxPerMember { get; set; } = 1;
    public int VoucherValidDays { get; set; } = 30;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Redemption> Redemptions { get; set; } = new List<Redemption>();

    // Stops two members both getting the last voucher
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
