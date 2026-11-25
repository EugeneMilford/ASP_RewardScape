namespace SpendWell.Api.Models;

/// <summary>
/// An outside business that honours SpendWell deals (coffee shop, cinema, etc.).
/// SpendWell pays the partner RandValuePerRedemption for every voucher they accept.
/// </summary>
public class Partner
{
    public int PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PartnerCategory Category { get; set; }
    public decimal RandValuePerRedemption { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
    public ICollection<PartnerSettlement> Settlements { get; set; } = new List<PartnerSettlement>();
}
