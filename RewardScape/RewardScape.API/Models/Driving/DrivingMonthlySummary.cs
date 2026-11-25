namespace SpendWell.Api.Models;

/// <summary>
/// A closed driving month. Created when the month is finalised and its points are awarded.
/// One row per member per month, so a month can never pay out twice.
/// </summary>
public class DrivingMonthlySummary
{
    public int DrivingMonthlySummaryId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int Year { get; set; }
    public int Month { get; set; }

    public int TripCount { get; set; }
    public decimal TotalDistanceKm { get; set; }

    /// <summary>Distance-weighted: a 300 km trip counts ten times more than a 30 km trip.</summary>
    public decimal AverageScore { get; set; }

    public int BasePoints { get; set; }
    public decimal MultiplierApplied { get; set; } = 1.0m;
    public int PointsAwarded { get; set; }

    public bool IsFinalised { get; set; }
    public DateTime? FinalisedAt { get; set; }
}
