namespace SpendWell.Api.Models;

/// <summary>
/// Monthly driving reward levels, e.g. average score 90+ = "Excellent" = 600 points.
/// The highest band the member's average reaches is the one that pays.
/// </summary>
public class DrivingRewardBand
{
    public int DrivingRewardBandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MinScore { get; set; }
    public int BasePoints { get; set; }
}
