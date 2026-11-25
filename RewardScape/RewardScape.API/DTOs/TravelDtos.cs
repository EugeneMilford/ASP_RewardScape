using System.ComponentModel.DataAnnotations;
using SpendWell.Api.Models;

namespace SpendWell.Api.DTOs;

// ---------- Providers & listings ----------

public record TravelProviderDto(
    int Id,
    string Name,
    string Type,
    bool IsActive);

public record CreateTravelProviderDto(
    [Required, StringLength(100)] string Name,
    TravelType Type);

public record TravelListingDto(
    int Id,
    int ProviderId,
    string ProviderName,
    string Type,
    string Title,
    string Description,
    string? Origin,
    string Destination,
    DateTime AvailableFrom,
    DateTime AvailableTo,
    decimal PricePerUnit,
    string PriceUnit,
    int TotalUnits,
    int MaxUnitsPerBooking,
    bool IsActive);

public record TravelListingQueryDto(
    TravelType? Type,
    string? Destination,
    DateTime? From,
    DateTime? To);

public record CreateTravelListingDto(
    [Required] int ProviderId,
    [Required, StringLength(150)] string Title,
    [StringLength(1000)] string Description,
    [StringLength(100)] string? Origin,
    [Required, StringLength(100)] string Destination,
    DateTime AvailableFrom,
    DateTime AvailableTo,
    [Range(0.01, 1_000_000)] decimal PricePerUnit,
    [Range(1, 10_000)] int TotalUnits,
    [Range(1, 50)] int MaxUnitsPerBooking);

public record AvailabilityDto(
    int ListingId,
    DateTime From,
    DateTime To,
    int TotalUnits,
    int UnitsAvailable,
    bool IsAvailable);

// ---------- Quote & booking ----------

/// <summary>StartDate/EndDate are needed for hotels and car hire; flights use the flight's own times.</summary>
public record TravelQuoteRequestDto(
    [Required] int MemberId,
    [Required] int ListingId,
    DateTime? StartDate,
    DateTime? EndDate,
    [Range(1, 50)] int Units);

/// <summary>Price after tier discount, points the trip will earn, and the cancellation deadline.</summary>
public record TravelQuoteDto(
    int ListingId,
    string Title,
    string Type,
    DateTime StartDate,
    DateTime EndDate,
    int Units,
    int Nights,
    decimal PricePerUnit,
    decimal BaseAmount,
    string Tier,
    decimal TierDiscountPercent,
    decimal DiscountAmount,
    decimal TotalToPay,
    int UnitsAvailable,
    int EstimatedPoints,
    DateTime FreeCancellationUntil,
    decimal LateCancellationFeePercent);

public record TravellerDto(
    [Required, StringLength(100)] string FullName,
    bool IsChild);

public record CreateTravelBookingDto(
    [Required] int MemberId,
    [Required, StringLength(10, MinimumLength = 10)] string AccountNumber,
    [Required] int ListingId,
    DateTime? StartDate,
    DateTime? EndDate,
    [Range(1, 50)] int Units,
    [Required, MinLength(1)] List<TravellerDto> Travellers);

public record TravelBookingDto(
    int Id,
    string BookingReference,
    int MemberId,
    string AccountNumber,
    int ListingId,
    string Title,
    string ProviderName,
    string Type,
    DateTime StartDate,
    DateTime EndDate,
    int Units,
    int Nights,
    decimal BaseAmount,
    decimal TierDiscountPercent,
    decimal DiscountAmount,
    decimal TotalPaid,
    decimal CancellationFee,
    decimal RefundedAmount,
    string Status,
    int PointsAwarded,
    DateTime BookedAt,
    DateTime? CancelledAt,
    DateTime? CompletedAt,
    IReadOnlyList<TravellerDto> Travellers);

public record TravelBookingResultDto(
    TravelBookingDto Booking,
    decimal AccountBalanceAfter,
    int EstimatedPoints,
    DateTime FreeCancellationUntil);

public record CancelTravelBookingResultDto(
    TravelBookingDto Booking,
    decimal CancellationFee,
    decimal AmountRefunded,
    decimal AccountBalanceAfter);

// ---------- Earn rules & jobs ----------

public record TravelEarnRuleDto(
    int Id,
    string Type,
    decimal PointsPerR10);

public record UpdateTravelEarnRuleDto(
    [Range(0, 100)] decimal PointsPerR10);

public record TravelCompletionRunDto(
    int BookingsCompleted,
    int PointsAwarded);
