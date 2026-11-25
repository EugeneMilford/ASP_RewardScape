namespace SpendWell.Api.Models;

/// <summary>
/// A monthly spend level. CumulativePoints is the TOTAL a member has earned for the month
/// once they reach this level (R500 = 100, R1 000 = 250 means reaching R1 000 adds 150 more).
/// </summary>
public class SpendMilestone
{
    public int SpendMilestoneId { get; set; }
    public decimal ThresholdAmount { get; set; }
    public int CumulativePoints { get; set; }
    public bool IsActive { get; set; } = true;
}
