namespace SpendWell.Api.Models;

/// <summary>An airline, hotel group or car hire company that sells through the app.</summary>
public class TravelProvider
{
    public int TravelProviderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TravelType Type { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TravelListing> Listings { get; set; } = new List<TravelListing>();
}
