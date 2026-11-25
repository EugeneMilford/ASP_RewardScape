namespace SpendWell.Api.Models;

/// <summary>
/// Something bookable:
/// - Flight: one flight. AvailableFrom/To are departure/arrival. TotalUnits = seats.
/// - Hotel: one room type. AvailableFrom/To is the window you can stay in. TotalUnits = rooms.
/// - CarHire: one car class at one branch. TotalUnits = cars.
/// Price is per seat, per room per night, or per car per day.
/// </summary>
public class TravelListing
{
    public int TravelListingId { get; set; }

    public int ProviderId { get; set; }
    public TravelProvider Provider { get; set; } = null!;

    public TravelType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Departure city for flights; null for hotels and car hire.</summary>
    public string? Origin { get; set; }
    public string Destination { get; set; } = string.Empty;

    public DateTime AvailableFrom { get; set; }
    public DateTime AvailableTo { get; set; }

    public decimal PricePerUnit { get; set; }
    public int TotalUnits { get; set; }
    public int MaxUnitsPerBooking { get; set; } = 4;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Updated on every booking. Changing a column bumps RowVersion, so two people booking
    /// the last room at the same moment can't both succeed.
    /// </summary>
    public DateTime? LastBookedAt { get; set; }

    public ICollection<TravelBooking> Bookings { get; set; } = new List<TravelBooking>();

    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
