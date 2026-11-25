namespace SpendWell.Api.Models;

public class Member
{
    public int MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public int TierId { get; set; }
    public Tier Tier { get; set; } = null!;

    /// <summary>
    /// Points the member still owes after a refund clawed back more points than they had left.
    /// New earnings pay this off first before creating a new points lot.
    /// </summary>
    public int PointsOwed { get; set; }

    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<PointsLot> PointsLots { get; set; } = new List<PointsLot>();
    public ICollection<PointsTransaction> PointsTransactions { get; set; } = new List<PointsTransaction>();
    public ICollection<Redemption> Redemptions { get; set; } = new List<Redemption>();
    public ICollection<TierHistory> TierHistory { get; set; } = new List<TierHistory>();

    // Shopping
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    // Travel
    public ICollection<TravelBooking> TravelBookings { get; set; } = new List<TravelBooking>();

    // Driving
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<DrivingTrip> DrivingTrips { get; set; } = new List<DrivingTrip>();
    public ICollection<DrivingMonthlySummary> DrivingSummaries { get; set; } = new List<DrivingMonthlySummary>();

    // Concurrency token: stops two requests changing PointsOwed/Tier at the same time
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
