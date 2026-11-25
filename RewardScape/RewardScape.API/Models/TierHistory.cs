namespace SpendWell.Api.Models;

public class TierHistory
{
    public int TierHistoryId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int FromTierId { get; set; }
    public Tier FromTier { get; set; } = null!;

    public int ToTierId { get; set; }
    public Tier ToTier { get; set; } = null!;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}
