using System.ComponentModel.DataAnnotations;

namespace SpendWell.Api.DTOs;

// ---------- Vehicles ----------

public record RegisterVehicleDto(
    [Required] int MemberId,
    [Required, StringLength(15)] string RegistrationNumber,
    [Required, StringLength(50)] string Make,
    [Required, StringLength(50)] string Model,
    [Range(1980, 2100)] int Year,
    [Required, StringLength(50)] string TelematicsDeviceId);

public record VehicleDto(
    int Id,
    int MemberId,
    string RegistrationNumber,
    string Make,
    string Model,
    int Year,
    string TelematicsDeviceId,
    bool IsActive,
    DateTime RegisteredAt);

// ---------- Trips ----------

/// <summary>Sent by the telematics device or phone app at the end of each trip.</summary>
public record UploadTripDto(
    [Required, StringLength(64)] string ExternalTripId,
    [Required, StringLength(50)] string TelematicsDeviceId,
    DateTime StartedAt,
    DateTime EndedAt,
    [Range(0.1, 2000)] decimal DistanceKm,
    [Range(0, 1000)] int HarshBrakingCount,
    [Range(0, 1000)] int HarshAccelerationCount,
    [Range(0, 1000)] int HarshCorneringCount,
    [Range(0, 86_400)] int SpeedingSeconds,
    [Range(0, 86_400)] int PhoneUseSeconds,
    [Range(0, 1440)] int NightDrivingMinutes);

/// <summary>How the trip score was worked out: 100 minus each penalty.</summary>
public record ScoreBreakdownDto(
    decimal BrakingPenalty,
    decimal AccelerationPenalty,
    decimal CorneringPenalty,
    decimal SpeedingPenalty,
    decimal PhoneUsePenalty,
    decimal NightDrivingPenalty,
    decimal Score);

public record DrivingTripDto(
    int Id,
    string ExternalTripId,
    int VehicleId,
    string RegistrationNumber,
    DateTime StartedAt,
    DateTime EndedAt,
    decimal DistanceKm,
    decimal Score,
    ScoreBreakdownDto Breakdown);

// ---------- Monthly rewards ----------

public record DrivingRewardBandDto(
    int Id,
    string Name,
    decimal MinScore,
    int BasePoints);

/// <summary>
/// This month's driving so far (live) or a closed month (IsFinalised = true):
/// "Average 84.2 over 312 km, Good band, 600 points at Gold. 5.8 more for Excellent."
/// </summary>
public record DrivingMonthSummaryDto(
    int MemberId,
    int Year,
    int Month,
    int TripCount,
    decimal TotalDistanceKm,
    decimal AverageScore,
    decimal MinimumDistanceKm,
    bool MeetsMinimumDistance,
    string? CurrentBand,
    int BasePoints,
    decimal EarnMultiplier,
    int Points,
    string? NextBand,
    decimal? NextBandMinScore,
    bool IsFinalised);

public record DrivingMonthRunResultDto(
    int Year,
    int Month,
    int MembersProcessed,
    int MembersRewarded,
    int PointsAwarded);
