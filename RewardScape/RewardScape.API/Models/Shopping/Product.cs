namespace SpendWell.Api.Models;

/// <summary>An item in the online shop catalogue.</summary>
public class Product
{
    public int ProductId { get; set; }

    /// <summary>Stock-keeping unit, unique across the catalogue (e.g. GRC-MILK-2L).</summary>
    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    // Two checkouts can't both take the last unit
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
