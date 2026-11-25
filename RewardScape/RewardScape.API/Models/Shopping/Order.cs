namespace SpendWell.Api.Models;

/// <summary>
/// A paid online order. Checkout also creates a Purchase (at the "SpendWell Online" store),
/// so online spending debits the account and counts toward monthly shopping milestones
/// exactly like a till purchase.
/// </summary>
public class Order
{
    public int OrderId { get; set; }

    /// <summary>e.g. ORD-261107-K7QM2X</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int? CartId { get; set; }
    public Cart? Cart { get; set; }

    /// <summary>The purchase that debited the account and earned the points.</summary>
    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Paid;
    public Address DeliveryAddress { get; set; } = new Address();

    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public DateTime StatusUpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
