namespace SpendWell.Api.Models;

/// <summary>
/// A product line on an order. Name, SKU and price are copied at checkout so the order
/// never changes if the product is renamed or repriced later.
/// </summary>
public class OrderItem
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
