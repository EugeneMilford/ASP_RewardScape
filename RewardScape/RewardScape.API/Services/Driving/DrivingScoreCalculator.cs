using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

/// <summary>
/// Turns a trip's raw telematics events into a 0–100 score.
/// Harsh events are counted per 100 km, so a long trip isn't punished for having more
/// chances to brake. Speeding, phone use and night driving are a share of trip time.
///
///   Score = 100
///         - 2.0 × harsh brakes per 100 km
///         - 1.5 × harsh accelerations per 100 km
///         - 1.0 × harsh corners per 100 km
///         - 1.5 × % of the trip spent speeding
///         - 2.0 × % of the trip spent on the phone
///         - 0.2 × % of the trip driven at night
/// </summary>
public static class DrivingScoreCalculator
{
    private const decimal BrakingWeight = 2.0m;
    private const decimal AccelerationWeight = 1.5m;
    private const decimal CorneringWeight = 1.0m;
    private const decimal SpeedingWeight = 1.5m;
    private const decimal PhoneUseWeight = 2.0m;
    private const decimal NightWeight = 0.2m;

    public static ScoreBreakdownDto Calculate(DrivingTrip trip)
    {
        return Calculate(
            trip.DistanceKm,
            trip.EndedAt - trip.StartedAt,
            trip.HarshBrakingCount,
            trip.HarshAccelerationCount,
            trip.HarshCorneringCount,
            trip.SpeedingSeconds,
            trip.PhoneUseSeconds,
            trip.NightDrivingMinutes);
    }

    public static ScoreBreakdownDto Calculate(
        decimal distanceKm,
        TimeSpan duration,
        int harshBraking,
        int harshAcceleration,
        int harshCornering,
        int speedingSeconds,
        int phoneUseSeconds,
        int nightDrivingMinutes)
    {
        decimal per100Km = distanceKm <= 0 ? 0m : 100m / distanceKm;
        decimal tripSeconds = (decimal)Math.Max(duration.TotalSeconds, 1d);

        decimal braking = Round1(harshBraking * per100Km * BrakingWeight);
        decimal acceleration = Round1(harshAcceleration * per100Km * AccelerationWeight);
        decimal cornering = Round1(harshCornering * per100Km * CorneringWeight);
        decimal speeding = Round1(speedingSeconds / tripSeconds * 100m * SpeedingWeight);
        decimal phoneUse = Round1(phoneUseSeconds / tripSeconds * 100m * PhoneUseWeight);
        decimal night = Round1(nightDrivingMinutes * 60m / tripSeconds * 100m * NightWeight);

        decimal score = 100m - braking - acceleration - cornering - speeding - phoneUse - night;
        score = Math.Clamp(Round1(score), 0m, 100m);

        return new ScoreBreakdownDto(braking, acceleration, cornering, speeding, phoneUse, night, score);
    }

    private static decimal Round1(decimal value)
    {
        return Math.Round(value, 1, MidpointRounding.AwayFromZero);
    }
}
