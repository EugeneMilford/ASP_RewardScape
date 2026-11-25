namespace SpendWell.Api.Models;

/// <summary>Delivery address. Stored inside the Orders table as an EF Core owned type.</summary>
public class Address
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string Suburb { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}
