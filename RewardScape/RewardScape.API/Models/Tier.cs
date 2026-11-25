namespace SpendWell.Api.Models;

public class Tier
{
    public int TierId { get; set; }
    public string Name { get; set; } = string.Empty;   // Blue, Silver, Gold, Diamond
    public int Rank { get; set; }                       // 1 = lowest, used to compare tiers

    /// <summary>Points earned in the last 12 months needed to reach this tier.</summary>
    public int MinPointsRolling12M { get; set; }

    /// <summary>Multiplier applied to all earned points (shopping, travel, driving), e.g. 1.5 for Gold.</summary>
    public decimal EarnMultiplier { get; set; } = 1.0m;

    /// <summary>Discount applied to the cash part of a deal, e.g. 10 = 10% off.</summary>
    public decimal DealDiscountPercent { get; set; }

    /// <summary>Discount on flights, hotels and car hire booked through the app.</summary>
    public decimal TravelDiscountPercent { get; set; }

    public ICollection<Member> Members { get; set; } = new List<Member>();
}
