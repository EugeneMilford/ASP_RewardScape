namespace SpendWell.Api.Models;

/// <summary>A member's car, linked to the telematics device (or phone app) that records its trips.</summary>
public class Vehicle
{
    public int VehicleId { get; set; }

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public string RegistrationNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }

    /// <summary>Unique ID of the tracker. Uploaded trips are matched to the vehicle by this.</summary>
    public string TelematicsDeviceId { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public ICollection<DrivingTrip> Trips { get; set; } = new List<DrivingTrip>();
}
