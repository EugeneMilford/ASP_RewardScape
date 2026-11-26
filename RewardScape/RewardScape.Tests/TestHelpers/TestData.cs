using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Tests.TestHelpers;

/// <summary>
/// Builders that put ready-made rows in the test database, so each test's
/// "Arrange" is a line or two. Seeded ids: tiers 1 Blue, 2 Silver, 3 Gold, 4 Diamond;
/// stores 1-3 physical, 4 online; travel providers 1 flight, 2 hotel, 3 car hire.
/// </summary>
public static class TestData
{
    public const int BlueTier = 1;
    public const int SilverTier = 2;
    public const int GoldTier = 3;
    public const int DiamondTier = 4;
    public const int VredenburgStore = 1;

    private static int _counter;

    private static int Next()
    {
        return Interlocked.Increment(ref _counter);
    }

    /// <summary>A member (loaded with Tier) and one account with the given balance.</summary>
    public static async Task<(Member Member, Account Account)> MemberWithAccountAsync(
        SpendWellDbContext db, decimal balance = 10_000m, int tierId = BlueTier)
    {
        int n = Next();

        var member = new Member
        {
            FullName = string.Format("Test Member {0}", n),
            Email = string.Format("member{0}@test.local", n),
            PhoneNumber = "0820000000",
            TierId = tierId,
            JoinedAt = DateTime.UtcNow
        };

        var account = new Account
        {
            AccountNumber = string.Format("63{0:D8}", n),
            Balance = balance,
            Member = member
        };

        db.Members.Add(member);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        await db.Entry(member).Reference(m => m.Tier).LoadAsync();
        return (member, account);
    }

    /// <summary>A till purchase request for one line of groceries.</summary>
    public static CreatePurchaseDto TillPurchase(Member member, Account account, decimal amount, string? slip = null)
    {
        return new CreatePurchaseDto(
            slip ?? string.Format("SLIP-{0}", Next()),
            member.MemberId,
            account.AccountNumber,
            VredenburgStore,
            new List<CreatePurchaseItemDto>
            {
                new CreatePurchaseItemDto("Groceries", ProductCategory.Groceries, 1, amount)
            });
    }

    /// <summary>A points lot added straight to the database (as if earned earlier).</summary>
    public static async Task<PointsLot> LotAsync(SpendWellDbContext db, Member member, int points, DateTime expiresAt)
    {
        var lot = new PointsLot
        {
            MemberId = member.MemberId,
            OriginalPoints = points,
            RemainingPoints = points,
            EarnedAt = expiresAt.AddMonths(-12),
            ExpiresAt = expiresAt
        };

        db.PointsLots.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    public static async Task<Deal> DealAsync(
        SpendWellDbContext db, int pointsCost = 150, decimal cashPrice = 0m, int minTierId = BlueTier,
        int stock = 10, int maxPerMember = 5)
    {
        var deal = new Deal
        {
            Title = string.Format("Test deal {0}", Next()),
            Description = "For tests",
            PointsCost = pointsCost,
            CashPrice = cashPrice,
            MinTierId = minTierId,
            StockRemaining = stock,
            MaxPerMember = maxPerMember,
            VoucherValidDays = 30,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            EndsAt = DateTime.UtcNow.AddDays(30),
            IsActive = true
        };

        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        await db.Entry(deal).Reference(d => d.MinTier).LoadAsync();
        return deal;
    }

    public static async Task<Product> ProductAsync(
        SpendWellDbContext db, decimal price, int stock = 50, ProductCategory category = ProductCategory.Groceries,
        string? name = null)
    {
        int n = Next();

        var product = new Product
        {
            Sku = string.Format("TST-{0}", n),
            Name = name ?? string.Format("Test product {0}", n),
            Description = "For tests",
            Category = category,
            Price = price,
            StockQuantity = stock,
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    public static AddressDto Address()
    {
        return new AddressDto("12 Main Road", null, "Vredenburg", "Vredenburg", "7380");
    }

    /// <summary>A hotel room type, bookable from today for a year.</summary>
    public static async Task<TravelListing> HotelAsync(SpendWellDbContext db, decimal pricePerNight = 1200m, int rooms = 10)
    {
        var listing = new TravelListing
        {
            ProviderId = 2,
            Type = TravelType.Hotel,
            Title = string.Format("Test hotel {0}", Next()),
            Description = "For tests",
            Destination = "Knysna",
            AvailableFrom = DateTime.UtcNow.Date,
            AvailableTo = DateTime.UtcNow.Date.AddDays(365),
            PricePerUnit = pricePerNight,
            TotalUnits = rooms,
            MaxUnitsPerBooking = 3,
            IsActive = true
        };

        db.TravelListings.Add(listing);
        await db.SaveChangesAsync();
        return listing;
    }

    /// <summary>A flight departing in 30 days.</summary>
    public static async Task<TravelListing> FlightAsync(SpendWellDbContext db, decimal pricePerSeat = 1450m, int seats = 100)
    {
        var departs = DateTime.UtcNow.Date.AddDays(30).AddHours(7);

        var listing = new TravelListing
        {
            ProviderId = 1,
            Type = TravelType.Flight,
            Title = string.Format("SB{0} Cape Town to Johannesburg", Next()),
            Description = "For tests",
            Origin = "Cape Town",
            Destination = "Johannesburg",
            AvailableFrom = departs,
            AvailableTo = departs.AddHours(2),
            PricePerUnit = pricePerSeat,
            TotalUnits = seats,
            MaxUnitsPerBooking = 6,
            IsActive = true
        };

        db.TravelListings.Add(listing);
        await db.SaveChangesAsync();
        return listing;
    }

    /// <summary>
    /// A confirmed booking added straight to the database with any dates, including past ones
    /// (the booking service won't book the past, but completion needs a finished trip).
    /// </summary>
    public static async Task<TravelBooking> BookingAsync(
        SpendWellDbContext db, Member member, Account account, TravelListing listing,
        DateTime start, DateTime end, decimal totalPaid)
    {
        var booking = new TravelBooking
        {
            BookingReference = string.Format("TRV-T{0:D5}", Next()),
            MemberId = member.MemberId,
            AccountId = account.AccountId,
            ListingId = listing.TravelListingId,
            Type = listing.Type,
            StartDate = start,
            EndDate = end,
            Units = 1,
            Nights = Math.Max((end.Date - start.Date).Days, 1),
            BaseAmount = totalPaid,
            TotalPaid = totalPaid,
            Status = TravelBookingStatus.Confirmed,
            BookedAt = start.AddDays(-30),
            Travellers = new List<TravelTraveller> { new TravelTraveller { FullName = member.FullName } }
        };

        db.TravelBookings.Add(booking);
        await db.SaveChangesAsync();
        return booking;
    }

    public static List<TravellerDto> Adults(int count)
    {
        var list = new List<TravellerDto>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new TravellerDto(string.Format("Traveller {0}", i), false));
        }
        return list;
    }

    public static async Task<Vehicle> VehicleAsync(SpendWellDbContext db, Member member)
    {
        int n = Next();

        var vehicle = new Vehicle
        {
            MemberId = member.MemberId,
            RegistrationNumber = string.Format("CA{0:D6}", n),
            Make = "Toyota",
            Model = "Corolla",
            Year = 2020,
            TelematicsDeviceId = string.Format("DEV-{0}", n),
            IsActive = true
        };

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle;
    }

    /// <summary>A scored trip added straight to the database on any date.</summary>
    public static async Task<DrivingTrip> TripAsync(
        SpendWellDbContext db, Vehicle vehicle, DateTime startedAt, decimal distanceKm, decimal score)
    {
        var trip = new DrivingTrip
        {
            ExternalTripId = string.Format("TRIP-{0}", Next()),
            VehicleId = vehicle.VehicleId,
            MemberId = vehicle.MemberId,
            StartedAt = startedAt,
            EndedAt = startedAt.AddHours(1),
            DistanceKm = distanceKm,
            Score = score
        };

        db.DrivingTrips.Add(trip);
        await db.SaveChangesAsync();
        return trip;
    }

    /// <summary>The first day of the month two months back: always closable by the driving job.</summary>
    public static DateTime TwoMonthsAgo()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-2);
    }

    public static async Task<int> SpendablePointsAsync(SpendWellDbContext db, int memberId)
    {
        var now = DateTime.UtcNow;

        int inLots = await db.PointsLots
            .Where(l => l.MemberId == memberId && !l.IsExpired && l.ExpiresAt > now)
            .SumAsync(l => (int?)l.RemainingPoints) ?? 0;

        int owed = await db.Members.Where(m => m.MemberId == memberId).Select(m => m.PointsOwed).FirstAsync();
        return inLots - owed;
    }
}
