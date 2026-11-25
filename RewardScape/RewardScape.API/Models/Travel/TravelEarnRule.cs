namespace SpendWell.Api.Models;

/// <summary>
/// How many points every R10 of travel spend earns, per travel type.
/// The member's tier multiplier is applied on top.
/// </summary>
public class TravelEarnRule
{
    public int TravelEarnRuleId { get; set; }
    public TravelType Type { get; set; }
    public decimal PointsPerR10 { get; set; }
}
