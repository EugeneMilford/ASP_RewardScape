namespace SpendWell.Api.Models;

/// <summary>
/// Records exactly which points lot paid for which redemption. Lets a cancelled
/// redemption put the points back into the same lots, keeping their original expiry dates.
/// </summary>
public class LotConsumption
{
    public int LotConsumptionId { get; set; }

    public int RedemptionId { get; set; }
    public Redemption Redemption { get; set; } = null!;

    public int PointsLotId { get; set; }
    public PointsLot PointsLot { get; set; } = null!;

    public int PointsTaken { get; set; }
}
