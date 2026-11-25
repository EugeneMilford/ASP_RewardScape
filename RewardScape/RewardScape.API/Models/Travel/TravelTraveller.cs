namespace SpendWell.Api.Models;

/// <summary>A person travelling on a booking (one per seat for flights, a lead guest for hotels).</summary>
public class TravelTraveller
{
    public int TravelTravellerId { get; set; }

    public int TravelBookingId { get; set; }
    public TravelBooking TravelBooking { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public bool IsChild { get; set; }
}
