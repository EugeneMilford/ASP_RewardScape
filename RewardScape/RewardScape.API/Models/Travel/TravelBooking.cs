namespace SpendWell.Api.Models;

/// <summary>
/// A paid booking. Money leaves the account when it's booked; points are only awarded
/// once the trip is Completed, so booking and cancelling can't farm points.
/// </summary>
public class TravelBooking
{
    public int TravelBookingId { get; set; }

    /// <summary>e.g. TRV-8K2MQ4</summary>
    public string BookingReference { get; set; } = string.Empty;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int ListingId { get; set; }
    public TravelListing Listing { get; set; } = null!;

    public TravelType Type { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>Seats, rooms or cars.</summary>
    public int Units { get; set; }

    /// <summary>Nights (hotel) or days (car hire). Always 1 for flights.</summary>
    public int Nights { get; set; }

    public decimal BaseAmount { get; set; }
    public decimal TierDiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPaid { get; set; }

    public decimal CancellationFee { get; set; }
    public decimal RefundedAmount { get; set; }

    public TravelBookingStatus Status { get; set; } = TravelBookingStatus.Confirmed;
    public int PointsAwarded { get; set; }

    public DateTime BookedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<TravelTraveller> Travellers { get; set; } = new List<TravelTraveller>();
}
