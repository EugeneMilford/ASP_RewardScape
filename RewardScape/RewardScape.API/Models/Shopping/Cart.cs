namespace SpendWell.Api.Models;

/// <summary>
/// A member's shopping cart. A member has at most one Active cart (enforced by a filtered
/// unique index); checking out turns it into an Order and marks it CheckedOut.
/// </summary>
public class Cart
{
    public int CartId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public CartStatus Status { get; set; } = CartStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedOutAt { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

    // A double-clicked "Pay now" can't check the same cart out twice
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
