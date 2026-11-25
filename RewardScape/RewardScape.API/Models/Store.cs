namespace SpendWell.Api.Models;

public class Store
{
    public int StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
