namespace SpendWell.Api.Models;

public class PurchaseItem
{
    public int PurchaseItemId { get; set; }

    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public string ProductName { get; set; } = string.Empty;
    public ProductCategory Category { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
