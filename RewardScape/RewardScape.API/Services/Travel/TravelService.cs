using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface ITravelService
{
    // Browsing
    Task<IReadOnlyList<TravelListingDto>> SearchAsync(TravelListingQueryDto query);
    Task<TravelListingDto> GetListingAsync(int listingId);
    Task<AvailabilityDto> GetAvailabilityAsync(int listingId, DateTime? from, DateTime? to);
    Task<TravelQuoteDto> QuoteAsync(TravelQuoteRequestDto dto);

    // Booking lifecycle
    Task<TravelBookingResultDto> BookAsync(CreateTravelBookingDto dto);
    Task<TravelBookingDto> GetBookingAsync(int bookingId);
    Task<IReadOnlyList<TravelBookingDto>> GetForMemberAsync(int memberId, TravelBookingStatus? status);
    Task<CancelTravelBookingResultDto> CancelAsync(int bookingId);
    Task<TravelBookingDto> CompleteAsync(int bookingId);
    Task<TravelCompletionRunDto> CompleteFinishedTripsAsync(CancellationToken cancellationToken = default);

    // Admin
    Task<IReadOnlyList<TravelProviderDto>> GetProvidersAsync();
    Task<TravelProviderDto> CreateProviderAsync(CreateTravelProviderDto dto);
    Task<TravelListingDto> CreateListingAsync(CreateTravelListingDto dto);
    Task<IReadOnlyList<TravelEarnRuleDto>> GetEarnRulesAsync();
    Task<TravelEarnRuleDto> UpdateEarnRuleAsync(TravelType type, UpdateTravelEarnRuleDto dto);
}

/// <summary>
/// Flights, hotels and car hire. Money leaves the account at booking; Travel points are
/// only paid once the trip is over, at R10 × the type's earn rate × the tier multiplier.
/// </summary>
public class TravelService : ITravelService
{
    public const int FreeCancellationHours = 48;
    public const decimal LateCancellationFeePercent = 50m;

    private readonly SpendWellDbContext _db;
    private readonly IPointsService _points;
    private readonly ITierService _tiers;
    private readonly ILogger<TravelService> _logger;

    public TravelService(SpendWellDbContext db, IPointsService points, ITierService tiers, ILogger<TravelService> logger)
    {
        _db = db;
        _points = points;
        _tiers = tiers;
        _logger = logger;
    }

    private record StayPeriod(DateTime Start, DateTime End, int Nights);

    private record PriceBreakdown(decimal BaseAmount, decimal DiscountPercent, decimal DiscountAmount, decimal Total);

    // ---------- Browsing ----------

    public async Task<IReadOnlyList<TravelListingDto>> SearchAsync(TravelListingQueryDto query)
    {
        var now = DateTime.UtcNow;

        var listings = _db.TravelListings
            .Include(l => l.Provider)
            .Where(l => l.IsActive && l.Provider.IsActive && l.AvailableTo > now);

        if (query.Type.HasValue)
        {
            listings = listings.Where(l => l.Type == query.Type.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Destination))
        {
            string destination = query.Destination.Trim();
            listings = listings.Where(l => l.Destination.Contains(destination));
        }

        if (query.From.HasValue)
        {
            listings = listings.Where(l => l.AvailableTo > query.From.Value);
        }

        if (query.To.HasValue)
        {
            listings = listings.Where(l => l.AvailableFrom < query.To.Value);
        }

        var list = await listings.ToListAsync();

        // Sorted in memory so the same code runs on SQL Server and on SQLite (tests),
        // which can't ORDER BY a decimal column
        return list
            .OrderBy(l => l.AvailableFrom)
            .ThenBy(l => l.PricePerUnit)
            .Select(l => l.ToDto())
            .ToList();
    }

    public async Task<TravelListingDto> GetListingAsync(int listingId)
    {
        var listing = await LoadListingAsync(listingId);
        return listing.ToDto();
    }

    public async Task<AvailabilityDto> GetAvailabilityAsync(int listingId, DateTime? from, DateTime? to)
    {
        var listing = await LoadListingAsync(listingId);
        var stay = ResolveDates(listing, from, to);
        int free = await UnitsFreeAsync(listing, stay.Start, stay.End);

        return new AvailabilityDto(listing.TravelListingId, stay.Start, stay.End, listing.TotalUnits, Math.Max(free, 0), free > 0);
    }

    /// <summary>Price, discount, points and cancellation terms, without booking anything.</summary>
    public async Task<TravelQuoteDto> QuoteAsync(TravelQuoteRequestDto dto)
    {
        var member = await LoadMemberAsync(dto.MemberId);
        var listing = await LoadListingAsync(dto.ListingId);

        var stay = ResolveDates(listing, dto.StartDate, dto.EndDate);
        int free = await UnitsFreeAsync(listing, stay.Start, stay.End);
        var price = CalculatePrice(listing, member.Tier, dto.Units, stay.Nights);
        var rule = await GetRuleAsync(listing.Type);

        return new TravelQuoteDto(
            listing.TravelListingId,
            listing.Title,
            listing.Type.ToString(),
            stay.Start,
            stay.End,
            dto.Units,
            stay.Nights,
            listing.PricePerUnit,
            price.BaseAmount,
            member.Tier.Name,
            price.DiscountPercent,
            price.DiscountAmount,
            price.Total,
            Math.Max(free, 0),
            CalculatePoints(price.Total, rule, member.Tier.EarnMultiplier),
            stay.Start.AddHours(-FreeCancellationHours),
            LateCancellationFeePercent);
    }

    // ---------- Booking lifecycle ----------

    public async Task<TravelBookingResultDto> BookAsync(CreateTravelBookingDto dto)
    {
        var member = await LoadMemberAsync(dto.MemberId);

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == dto.AccountNumber)
            ?? throw NotFoundException.For("Account", dto.AccountNumber);

        if (account.MemberId != member.MemberId)
        {
            throw new BusinessRuleException("This account does not belong to the member.");
        }

        var listing = await LoadListingAsync(dto.ListingId);

        if (!listing.IsActive || !listing.Provider.IsActive)
        {
            throw new BusinessRuleException("This listing is not available for booking.");
        }

        if (dto.Units > listing.MaxUnitsPerBooking)
        {
            throw new BusinessRuleException(string.Format(
                "You can book at most {0} {1} at a time.", listing.MaxUnitsPerBooking, UnitName(listing.Type)));
        }

        var stay = ResolveDates(listing, dto.StartDate, dto.EndDate);
        var now = DateTime.UtcNow;

        if (stay.Start <= now)
        {
            throw new BusinessRuleException("You can't book something that has already started.");
        }

        ValidateTravellers(listing.Type, dto.Units, dto.Travellers);

        var price = CalculatePrice(listing, member.Tier, dto.Units, stay.Nights);
        var rule = await GetRuleAsync(listing.Type);

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        int free = await UnitsFreeAsync(listing, stay.Start, stay.End);
        if (free < dto.Units)
        {
            throw new BusinessRuleException(string.Format(
                "Only {0} {1} left for those dates.", Math.Max(free, 0), UnitName(listing.Type)));
        }

        // Touching the listing bumps its RowVersion, so two bookings racing for the
        // last room can't both save: the second one gets a 409 and can try again.
        listing.LastBookedAt = now;

        var booking = new TravelBooking
        {
            BookingReference = await GenerateBookingReferenceAsync(),
            Member = member,
            Account = account,
            Listing = listing,
            Type = listing.Type,
            StartDate = stay.Start,
            EndDate = stay.End,
            Units = dto.Units,
            Nights = stay.Nights,
            BaseAmount = price.BaseAmount,
            TierDiscountPercent = price.DiscountPercent,
            DiscountAmount = price.DiscountAmount,
            TotalPaid = price.Total,
            Status = TravelBookingStatus.Confirmed,
            BookedAt = now,
            Travellers = dto.Travellers.Select(t => new TravelTraveller
            {
                FullName = t.FullName.Trim(),
                IsChild = t.IsChild
            }).ToList()
        };
        _db.TravelBookings.Add(booking);

        var debit = AccountPosting.Debit(account, price.Total, AccountTransactionType.TravelPayment,
            booking.BookingReference, string.Format("{0}: {1}", listing.Type, listing.Title));
        debit.TravelBooking = booking;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new TravelBookingResultDto(
            booking.ToDto(),
            account.Balance,
            CalculatePoints(price.Total, rule, member.Tier.EarnMultiplier),
            stay.Start.AddHours(-FreeCancellationHours));
    }

    public async Task<TravelBookingDto> GetBookingAsync(int bookingId)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.TravelBookingId == bookingId)
            ?? throw NotFoundException.For("Travel booking", bookingId);

        return booking.ToDto();
    }

    public async Task<IReadOnlyList<TravelBookingDto>> GetForMemberAsync(int memberId, TravelBookingStatus? status)
    {
        bool exists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!exists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        var query = LoadBookingQuery().Where(b => b.MemberId == memberId);
        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        var list = await query.OrderByDescending(b => b.StartDate).ToListAsync();
        return list.Select(b => b.ToDto()).ToList();
    }

    /// <summary>
    /// Free cancellation up to 48 hours before the trip starts; after that, 50% is kept.
    /// The released seats/rooms become bookable again straight away.
    /// </summary>
    public async Task<CancelTravelBookingResultDto> CancelAsync(int bookingId)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.TravelBookingId == bookingId)
            ?? throw NotFoundException.For("Travel booking", bookingId);

        switch (booking.Status)
        {
            case TravelBookingStatus.Confirmed:
                break;
            case TravelBookingStatus.Cancelled:
                throw new BusinessRuleException("This booking is already cancelled.");
            case TravelBookingStatus.Completed:
                throw new BusinessRuleException("This trip has already happened and can't be cancelled.");
        }

        var now = DateTime.UtcNow;
        if (now >= booking.StartDate)
        {
            throw new BusinessRuleException("This trip has already started and can't be cancelled.");
        }

        bool isFree = now < booking.StartDate.AddHours(-FreeCancellationHours);
        decimal fee = isFree
            ? 0m
            : Math.Round(booking.TotalPaid * LateCancellationFeePercent / 100m, 2, MidpointRounding.AwayFromZero);
        decimal refund = booking.TotalPaid - fee;

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        if (refund > 0)
        {
            var credit = AccountPosting.Credit(booking.Account, refund, AccountTransactionType.TravelRefund,
                booking.BookingReference,
                string.Format("Cancelled {0}{1}", booking.Listing.Title, isFree ? string.Empty : " (late cancellation fee kept)"));
            credit.TravelBooking = booking;
        }

        booking.Status = TravelBookingStatus.Cancelled;
        booking.CancellationFee = fee;
        booking.RefundedAmount = refund;
        booking.CancelledAt = now;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new CancelTravelBookingResultDto(booking.ToDto(), fee, refund, booking.Account.Balance);
    }

    /// <summary>
    /// Marks a finished trip Completed and pays its Travel points. Normally run by the
    /// background job, but can be triggered by hand (e.g. after a manual check-in).
    /// </summary>
    public async Task<TravelBookingDto> CompleteAsync(int bookingId)
    {
        var booking = await LoadBookingQuery().FirstOrDefaultAsync(b => b.TravelBookingId == bookingId)
            ?? throw NotFoundException.For("Travel booking", bookingId);

        if (booking.Status != TravelBookingStatus.Confirmed)
        {
            throw new BusinessRuleException(string.Format(
                "Only confirmed bookings can be completed. This one is {0}.", booking.Status));
        }

        var now = DateTime.UtcNow;
        if (booking.EndDate > now)
        {
            throw new BusinessRuleException(string.Format(
                "This trip only ends on {0:yyyy-MM-dd HH:mm}.", booking.EndDate));
        }

        var member = await LoadMemberAsync(booking.MemberId);
        var rule = await GetRuleAsync(booking.Type);
        decimal multiplier = member.Tier.EarnMultiplier;
        int points = CalculatePoints(booking.TotalPaid - booking.RefundedAmount, rule, multiplier);

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        booking.Status = TravelBookingStatus.Completed;
        booking.CompletedAt = now;
        booking.PointsAwarded = points;

        if (points > 0)
        {
            await _points.AwardAsync(member, points, multiplier, RewardCategory.Travel, "TravelBooking", booking.TravelBookingId,
                string.Format("Completed {0} ({1})", booking.Listing.Title, booking.BookingReference));
        }

        await _db.SaveChangesAsync();

        if (points > 0)
        {
            var newTier = await _tiers.UpgradeIfQualifiedAsync(member);
            if (newTier != null)
            {
                await _db.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();
        return booking.ToDto();
    }

    /// <summary>Completes every confirmed booking whose end date has passed. Safe to run repeatedly.</summary>
    public async Task<TravelCompletionRunDto> CompleteFinishedTripsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var ids = await _db.TravelBookings
            .Where(b => b.Status == TravelBookingStatus.Confirmed && b.EndDate <= now)
            .Select(b => b.TravelBookingId)
            .ToListAsync(cancellationToken);

        int completed = 0;
        int points = 0;

        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var booking = await CompleteAsync(id);
                completed++;
                points += booking.PointsAwarded;
            }
            catch (Exception ex) when (ex is BusinessRuleException || ex is DbUpdateException)
            {
                // One bad booking mustn't stop the rest. Clear half-made changes before the next one.
                _logger.LogWarning(ex, "Could not complete travel booking {BookingId}", id);
                _db.ChangeTracker.Clear();
            }
        }

        return new TravelCompletionRunDto(completed, points);
    }

    // ---------- Admin ----------

    public async Task<IReadOnlyList<TravelProviderDto>> GetProvidersAsync()
    {
        var providers = await _db.TravelProviders.OrderBy(p => p.Type).ThenBy(p => p.Name).ToListAsync();
        return providers.Select(p => p.ToDto()).ToList();
    }

    public async Task<TravelProviderDto> CreateProviderAsync(CreateTravelProviderDto dto)
    {
        var provider = new TravelProvider
        {
            Name = dto.Name.Trim(),
            Type = dto.Type,
            IsActive = true
        };

        _db.TravelProviders.Add(provider);
        await _db.SaveChangesAsync();

        return provider.ToDto();
    }

    public async Task<TravelListingDto> CreateListingAsync(CreateTravelListingDto dto)
    {
        var provider = await _db.TravelProviders.FirstOrDefaultAsync(p => p.TravelProviderId == dto.ProviderId)
            ?? throw NotFoundException.For("Travel provider", dto.ProviderId);

        if (dto.AvailableTo <= dto.AvailableFrom)
        {
            throw new BusinessRuleException("AvailableTo must be after AvailableFrom.");
        }

        if (provider.Type == TravelType.Flight && string.IsNullOrWhiteSpace(dto.Origin))
        {
            throw new BusinessRuleException("A flight needs an origin city.");
        }

        if (dto.MaxUnitsPerBooking > dto.TotalUnits)
        {
            throw new BusinessRuleException("MaxUnitsPerBooking can't be more than TotalUnits.");
        }

        var listing = new TravelListing
        {
            Provider = provider,
            Type = provider.Type,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            Origin = provider.Type == TravelType.Flight ? dto.Origin : null,
            Destination = dto.Destination.Trim(),
            AvailableFrom = dto.AvailableFrom,
            AvailableTo = dto.AvailableTo,
            PricePerUnit = dto.PricePerUnit,
            TotalUnits = dto.TotalUnits,
            MaxUnitsPerBooking = dto.MaxUnitsPerBooking,
            IsActive = true
        };

        _db.TravelListings.Add(listing);
        await _db.SaveChangesAsync();

        return listing.ToDto();
    }

    public async Task<IReadOnlyList<TravelEarnRuleDto>> GetEarnRulesAsync()
    {
        var rules = await _db.TravelEarnRules.OrderBy(r => r.TravelEarnRuleId).ToListAsync();
        return rules.Select(r => r.ToDto()).ToList();
    }

    /// <summary>Changes apply to trips completed from now on, not to points already paid.</summary>
    public async Task<TravelEarnRuleDto> UpdateEarnRuleAsync(TravelType type, UpdateTravelEarnRuleDto dto)
    {
        var rule = await GetRuleAsync(type);
        rule.PointsPerR10 = dto.PointsPerR10;
        await _db.SaveChangesAsync();
        return rule.ToDto();
    }

    // ---------- Rules ----------

    /// <summary>
    /// Every full R10 earns the type's rate, then the tier multiplier applies.
    /// R2 890 hotel stay at Gold: 289 × 3 = 867 base × 1.5 = 1 300 points.
    /// </summary>
    public static int CalculatePoints(decimal amountPaid, TravelEarnRule rule, decimal multiplier)
    {
        if (amountPaid <= 0)
        {
            return 0;
        }

        decimal basePoints = Math.Floor(amountPaid / 10m) * rule.PointsPerR10;
        return (int)Math.Floor(basePoints * multiplier);
    }

    private static PriceBreakdown CalculatePrice(TravelListing listing, Tier tier, int units, int nights)
    {
        decimal baseAmount = Math.Round(listing.PricePerUnit * units * nights, 2);
        decimal discount = Math.Round(baseAmount * tier.TravelDiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new PriceBreakdown(baseAmount, tier.TravelDiscountPercent, discount, baseAmount - discount);
    }

    /// <summary>
    /// Flights use the flight's own times. Hotels and car hire need dates inside the
    /// listing's window; the number of nights/days is what gets charged.
    /// </summary>
    private static StayPeriod ResolveDates(TravelListing listing, DateTime? start, DateTime? end)
    {
        switch (listing.Type)
        {
            case TravelType.Flight:
                return new StayPeriod(listing.AvailableFrom, listing.AvailableTo, 1);

            case TravelType.Hotel:
            case TravelType.CarHire:
                if (!start.HasValue || !end.HasValue)
                {
                    throw new BusinessRuleException(string.Format(
                        "Choose a start and end date for this {0}.", listing.Type == TravelType.Hotel ? "stay" : "car hire"));
                }

                var startDate = start.Value.Date;
                var endDate = end.Value.Date;

                if (endDate <= startDate)
                {
                    throw new BusinessRuleException("The end date must be after the start date.");
                }

                if (startDate < listing.AvailableFrom.Date || endDate > listing.AvailableTo.Date)
                {
                    throw new BusinessRuleException(string.Format(
                        "This listing is only available from {0:yyyy-MM-dd} to {1:yyyy-MM-dd}.",
                        listing.AvailableFrom, listing.AvailableTo));
                }

                return new StayPeriod(startDate, endDate, (endDate - startDate).Days);

            default:
                throw new BusinessRuleException("Unknown travel type.");
        }
    }

    /// <summary>
    /// How many seats/rooms/cars are still free for these dates.
    /// Hotels and cars: walk each night of the stay and find the busiest one, because a
    /// room is only free for the whole stay if it's free on every night of it.
    /// </summary>
    private async Task<int> UnitsFreeAsync(TravelListing listing, DateTime start, DateTime end)
    {
        var bookings = await _db.TravelBookings
            .Where(b => b.ListingId == listing.TravelListingId
                && b.Status != TravelBookingStatus.Cancelled
                && b.StartDate < end
                && b.EndDate > start)
            .Select(b => new { b.StartDate, b.EndDate, b.Units })
            .ToListAsync();

        switch (listing.Type)
        {
            case TravelType.Flight:
                return listing.TotalUnits - bookings.Sum(b => b.Units);

            default:
                int busiestNight = 0;
                for (var night = start; night < end; night = night.AddDays(1))
                {
                    int used = bookings
                        .Where(b => b.StartDate <= night && b.EndDate > night)
                        .Sum(b => b.Units);

                    if (used > busiestNight)
                    {
                        busiestNight = used;
                    }
                }
                return listing.TotalUnits - busiestNight;
        }
    }

    private static void ValidateTravellers(TravelType type, int units, List<TravellerDto> travellers)
    {
        if (travellers.Count == 0)
        {
            throw new BusinessRuleException("Add at least one traveller.");
        }

        if (travellers.All(t => t.IsChild))
        {
            throw new BusinessRuleException("At least one traveller must be an adult.");
        }

        switch (type)
        {
            case TravelType.Flight:
                if (travellers.Count != units)
                {
                    throw new BusinessRuleException(string.Format(
                        "Add one traveller per seat: {0} seats, {1} travellers.", units, travellers.Count));
                }
                break;
            case TravelType.Hotel:
                if (travellers.Count > units * 4)
                {
                    throw new BusinessRuleException(string.Format("{0} room(s) sleep at most {1} people.", units, units * 4));
                }
                break;
            case TravelType.CarHire:
                break;
        }
    }

    private static string UnitName(TravelType type)
    {
        switch (type)
        {
            case TravelType.Flight:
                return "seats";
            case TravelType.Hotel:
                return "rooms";
            case TravelType.CarHire:
                return "cars";
            default:
                return "units";
        }
    }

    // ---------- Loading ----------

    private async Task<Member> LoadMemberAsync(int memberId)
    {
        return await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);
    }

    private async Task<TravelListing> LoadListingAsync(int listingId)
    {
        return await _db.TravelListings
            .Include(l => l.Provider)
            .FirstOrDefaultAsync(l => l.TravelListingId == listingId)
            ?? throw NotFoundException.For("Travel listing", listingId);
    }

    private async Task<TravelEarnRule> GetRuleAsync(TravelType type)
    {
        return await _db.TravelEarnRules.FirstOrDefaultAsync(r => r.Type == type)
            ?? throw new NotFoundException(string.Format("No earn rule is set up for {0}.", type));
    }

    private IQueryable<TravelBooking> LoadBookingQuery()
    {
        return _db.TravelBookings
            .Include(b => b.Account)
            .Include(b => b.Listing).ThenInclude(l => l.Provider)
            .Include(b => b.Travellers);
    }

    private async Task<string> GenerateBookingReferenceAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string candidate = string.Format("TRV-{0}", CodeGenerator.Random(6));

            bool taken = await _db.TravelBookings.AnyAsync(b => b.BookingReference == candidate);
            if (!taken)
            {
                return candidate;
            }
        }

        throw new ConflictException("Could not generate a booking reference. Please try again.");
    }
}
