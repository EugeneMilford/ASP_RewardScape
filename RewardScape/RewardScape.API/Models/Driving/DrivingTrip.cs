namespace SpendWell.Api.Models;

/// <summary>
/// One trip uploaded by the telematics device. The raw events are stored so the score can
/// always be recalculated and explained; Score is saved for fast monthly averages.
/// </summary>
public class DrivingTrip
{
    public int DrivingTripId { get; set; }

    /// <summary>The device's own trip ID. Unique, so a re-sent upload is rejected, not double-counted.</summary>
    public string ExternalTripId { get; set; } = string.Empty;

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public decimal DistanceKm { get; set; }

    public int HarshBrakingCount { get; set; }
    public int HarshAccelerationCount { get; set; }
    public int HarshCorneringCount { get; set; }
    public int SpeedingSeconds { get; set; }
    public int PhoneUseSeconds { get; set; }
    public int NightDrivingMinutes { get; set; }

    /// <summary>0–100, higher is safer.</summary>
    public decimal Score { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
