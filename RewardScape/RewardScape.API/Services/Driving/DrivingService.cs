using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public static class DrivingRules
{
    /// <summary>Drive at least this far in a month to qualify for driving points.</summary>
    public const decimal MinimumMonthlyKm = 200m;

    /// <summary>Days after month-end that late trips can still upload before the month closes.</summary>
    public const int FinaliseGraceDays = 2;

    public const int MaxActiveVehiclesPerMember = 3;
    public const decimal MaxRealisticAverageKmh = 250m;
}

public interface IDrivingService
{
    Task<VehicleDto> RegisterVehicleAsync(RegisterVehicleDto dto);
    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(int memberId);
    Task<VehicleDto> DeactivateVehicleAsync(int vehicleId);

    Task<DrivingTripDto> UploadTripAsync(UploadTripDto dto);
    Task<PagedResult<DrivingTripDto>> GetTripsAsync(int memberId, int page, int pageSize);

    Task<DrivingMonthSummaryDto> GetMonthSummaryAsync(int memberId, int? year, int? month);
    Task<IReadOnlyList<DrivingRewardBandDto>> GetBandsAsync();
    Task<DrivingMonthRunResultDto> FinaliseMonthAsync(int year, int month);
}

/// <summary>
/// Driving rewards: every trip is scored, and once a month closes the member's
/// distance-weighted average score picks a reward band. Points = band × tier multiplier,
/// only if they drove at least 200 km that month.
/// </summary>
public class DrivingService : IDrivingService
{
    private readonly SpendWellDbContext _db;
    private readonly IPointsService _points;
    private readonly ITierService _tiers;
    private readonly ILogger<DrivingService> _logger;

    public DrivingService(SpendWellDbContext db, IPointsService points, ITierService tiers, ILogger<DrivingService> logger)
    {
        _db = db;
        _points = points;
        _tiers = tiers;
        _logger = logger;
    }

    private record MonthCalculation(int TripCount, decimal TotalKm, decimal AverageScore);

    // ---------- Vehicles ----------

    public async Task<VehicleDto> RegisterVehicleAsync(RegisterVehicleDto dto)
    {
        bool memberExists = await _db.Members.AnyAsync(m => m.MemberId == dto.MemberId);
        if (!memberExists)
        {
            throw NotFoundException.For("Member", dto.MemberId);
        }

        string registration = dto.RegistrationNumber.Replace(" ", string.Empty).Trim().ToUpperInvariant();
        string deviceId = dto.TelematicsDeviceId.Trim();

        int activeVehicles = await _db.Vehicles.CountAsync(v => v.MemberId == dto.MemberId && v.IsActive);
        if (activeVehicles >= DrivingRules.MaxActiveVehiclesPerMember)
        {
            throw new BusinessRuleException(string.Format(
                "You can have at most {0} active vehicles.", DrivingRules.MaxActiveVehiclesPerMember));
        }

        if (await _db.Vehicles.AnyAsync(v => v.RegistrationNumber == registration))
        {
            throw new ConflictException(string.Format("Vehicle {0} is already registered.", registration));
        }

        if (await _db.Vehicles.AnyAsync(v => v.TelematicsDeviceId == deviceId))
        {
            throw new ConflictException(string.Format("Device {0} is already linked to a vehicle.", deviceId));
        }

        var vehicle = new Vehicle
        {
            MemberId = dto.MemberId,
            RegistrationNumber = registration,
            Make = dto.Make.Trim(),
            Model = dto.Model.Trim(),
            Year = dto.Year,
            TelematicsDeviceId = deviceId,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        };

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();

        return vehicle.ToDto();
    }

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(int memberId)
    {
        bool memberExists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!memberExists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        var vehicles = await _db.Vehicles
            .Where(v => v.MemberId == memberId)
            .OrderByDescending(v => v.IsActive)
            .ThenBy(v => v.RegisteredAt)
            .ToListAsync();

        return vehicles.Select(v => v.ToDto()).ToList();
    }

    /// <summary>Sold or replaced car. Its trips stay on record; new uploads from its device are refused.</summary>
    public async Task<VehicleDto> DeactivateVehicleAsync(int vehicleId)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId)
            ?? throw NotFoundException.For("Vehicle", vehicleId);

        vehicle.IsActive = false;
        await _db.SaveChangesAsync();

        return vehicle.ToDto();
    }

    // ---------- Trips ----------

    /// <summary>
    /// Validates and scores an uploaded trip. Re-sending the same ExternalTripId is rejected
    /// with 409, so a device retrying after a dropped connection can't double-count.
    /// </summary>
    public async Task<DrivingTripDto> UploadTripAsync(UploadTripDto dto)
    {
        var now = DateTime.UtcNow;

        if (dto.EndedAt <= dto.StartedAt)
        {
            throw new BusinessRuleException("A trip must end after it starts.");
        }

        var duration = dto.EndedAt - dto.StartedAt;

        if (duration > TimeSpan.FromHours(24))
        {
            throw new BusinessRuleException("A single trip can't be longer than 24 hours.");
        }

        if (dto.EndedAt > now.AddMinutes(5))
        {
            throw new BusinessRuleException("A trip can't end in the future.");
        }

        decimal averageKmh = dto.DistanceKm / (decimal)duration.TotalHours;
        if (averageKmh > DrivingRules.MaxRealisticAverageKmh)
        {
            throw new BusinessRuleException(string.Format(
                "An average speed of {0:N0} km/h isn't realistic. The trip was rejected.", averageKmh));
        }

        if (dto.SpeedingSeconds > duration.TotalSeconds || dto.PhoneUseSeconds > duration.TotalSeconds)
        {
            throw new BusinessRuleException("Speeding or phone-use time can't be longer than the trip.");
        }

        if (dto.NightDrivingMinutes > duration.TotalMinutes)
        {
            throw new BusinessRuleException("Night-driving time can't be longer than the trip.");
        }

        bool duplicate = await _db.DrivingTrips.AnyAsync(t => t.ExternalTripId == dto.ExternalTripId);
        if (duplicate)
        {
            throw new ConflictException(string.Format("Trip {0} has already been uploaded.", dto.ExternalTripId));
        }

        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.TelematicsDeviceId == dto.TelematicsDeviceId && v.IsActive)
            ?? throw new NotFoundException(string.Format(
                "No active vehicle is linked to device {0}.", dto.TelematicsDeviceId));

        bool monthClosed = await _db.DrivingMonthlySummaries.AnyAsync(s =>
            s.MemberId == vehicle.MemberId
            && s.Year == dto.StartedAt.Year
            && s.Month == dto.StartedAt.Month
            && s.IsFinalised);

        if (monthClosed)
        {
            throw new BusinessRuleException(string.Format(
                "{0:MMMM yyyy} has already been closed and paid out.", dto.StartedAt));
        }

        var breakdown = DrivingScoreCalculator.Calculate(
            dto.DistanceKm,
            duration,
            dto.HarshBrakingCount,
            dto.HarshAccelerationCount,
            dto.HarshCorneringCount,
            dto.SpeedingSeconds,
            dto.PhoneUseSeconds,
            dto.NightDrivingMinutes);

        var trip = new DrivingTrip
        {
            ExternalTripId = dto.ExternalTripId,
            Vehicle = vehicle,
            MemberId = vehicle.MemberId,
            StartedAt = dto.StartedAt,
            EndedAt = dto.EndedAt,
            DistanceKm = dto.DistanceKm,
            HarshBrakingCount = dto.HarshBrakingCount,
            HarshAccelerationCount = dto.HarshAccelerationCount,
            HarshCorneringCount = dto.HarshCorneringCount,
            SpeedingSeconds = dto.SpeedingSeconds,
            PhoneUseSeconds = dto.PhoneUseSeconds,
            NightDrivingMinutes = dto.NightDrivingMinutes,
            Score = breakdown.Score,
            UploadedAt = now
        };

        _db.DrivingTrips.Add(trip);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException
            && ex.InnerException != null
            && ex.InnerException.Message.Contains("ExternalTripId"))
        {
            throw new ConflictException(string.Format("Trip {0} has already been uploaded.", dto.ExternalTripId));
        }

        return trip.ToDto();
    }

    public async Task<PagedResult<DrivingTripDto>> GetTripsAsync(int memberId, int page, int pageSize)
    {
        bool memberExists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!memberExists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.DrivingTrips.Include(t => t.Vehicle).Where(t => t.MemberId == memberId);
        int total = await query.CountAsync();

        var trips = await query
            .OrderByDescending(t => t.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<DrivingTripDto>(trips.Select(t => t.ToDto()).ToList(), page, pageSize, total);
    }

    // ---------- Monthly rewards ----------

    /// <summary>
    /// Live view of a month in progress, or the stored result of a closed month.
    /// </summary>
    public async Task<DrivingMonthSummaryDto> GetMonthSummaryAsync(int memberId, int? year, int? month)
    {
        var member = await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);

        var now = DateTime.UtcNow;
        int y = year ?? now.Year;
        int mth = month ?? now.Month;
        EnsureValidMonth(mth);

        var bands = await LoadBandsAsync();

        var closed = await _db.DrivingMonthlySummaries
            .FirstOrDefaultAsync(s => s.MemberId == memberId && s.Year == y && s.Month == mth && s.IsFinalised);

        if (closed != null)
        {
            var closedBand = PickBand(bands, closed.AverageScore);

            return new DrivingMonthSummaryDto(
                memberId, y, mth,
                closed.TripCount,
                closed.TotalDistanceKm,
                closed.AverageScore,
                DrivingRules.MinimumMonthlyKm,
                closed.TotalDistanceKm >= DrivingRules.MinimumMonthlyKm,
                closedBand != null ? closedBand.Name : null,
                closed.BasePoints,
                closed.MultiplierApplied,
                closed.PointsAwarded,
                null,
                null,
                true);
        }

        var calc = await CalculateMonthAsync(memberId, y, mth);
        var band = PickBand(bands, calc.AverageScore);
        var nextBand = bands
            .Where(b => b.MinScore > calc.AverageScore)
            .OrderBy(b => b.MinScore)
            .FirstOrDefault();

        bool meetsDistance = calc.TotalKm >= DrivingRules.MinimumMonthlyKm;
        int basePoints = meetsDistance && band != null ? band.BasePoints : 0;
        decimal multiplier = member.Tier.EarnMultiplier;

        return new DrivingMonthSummaryDto(
            memberId, y, mth,
            calc.TripCount,
            calc.TotalKm,
            calc.AverageScore,
            DrivingRules.MinimumMonthlyKm,
            meetsDistance,
            band != null ? band.Name : null,
            basePoints,
            multiplier,
            (int)Math.Floor(basePoints * multiplier),
            nextBand != null ? nextBand.Name : null,
            nextBand != null ? nextBand.MinScore : null,
            false);
    }

    public async Task<IReadOnlyList<DrivingRewardBandDto>> GetBandsAsync()
    {
        var bands = await LoadBandsAsync();
        return bands.Select(b => b.ToDto()).ToList();
    }

    /// <summary>
    /// Closes a month for every member who drove in it and pays their driving points.
    /// Each member is closed in their own transaction; already-closed members are skipped,
    /// so the job is safe to run more than once.
    /// </summary>
    public async Task<DrivingMonthRunResultDto> FinaliseMonthAsync(int year, int month)
    {
        EnsureValidMonth(month);

        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var closesFrom = monthEnd.AddDays(DrivingRules.FinaliseGraceDays);

        if (DateTime.UtcNow < closesFrom)
        {
            throw new BusinessRuleException(string.Format(
                "{0:MMMM yyyy} can only be closed from {1:yyyy-MM-dd}, so late trips can still upload.",
                monthStart, closesFrom));
        }

        var memberIds = await _db.DrivingTrips
            .Where(t => t.StartedAt >= monthStart && t.StartedAt < monthEnd)
            .Select(t => t.MemberId)
            .Distinct()
            .ToListAsync();

        var bands = await LoadBandsAsync();

        int processed = 0;
        int rewarded = 0;
        int totalPoints = 0;

        foreach (var memberId in memberIds)
        {
            try
            {
                int? awarded = await FinaliseMemberMonthAsync(memberId, year, month, bands);
                if (awarded == null)
                {
                    continue; // already closed
                }

                processed++;
                if (awarded.Value > 0)
                {
                    rewarded++;
                    totalPoints += awarded.Value;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Could not close driving month {Year}-{Month} for member {MemberId}", year, month, memberId);
                _db.ChangeTracker.Clear();
            }
        }

        return new DrivingMonthRunResultDto(year, month, processed, rewarded, totalPoints);
    }

    /// <summary>Returns the points awarded, or null if this member's month was already closed.</summary>
    private async Task<int?> FinaliseMemberMonthAsync(int memberId, int year, int month, IReadOnlyList<DrivingRewardBand> bands)
    {
        var summary = await _db.DrivingMonthlySummaries
            .FirstOrDefaultAsync(s => s.MemberId == memberId && s.Year == year && s.Month == month);

        if (summary != null && summary.IsFinalised)
        {
            return null;
        }

        var member = await _db.Members.Include(x => x.Tier).FirstAsync(x => x.MemberId == memberId);
        var calc = await CalculateMonthAsync(memberId, year, month);
        var band = PickBand(bands, calc.AverageScore);

        bool meetsDistance = calc.TotalKm >= DrivingRules.MinimumMonthlyKm;
        int basePoints = meetsDistance && band != null ? band.BasePoints : 0;
        decimal multiplier = member.Tier.EarnMultiplier;
        int points = (int)Math.Floor(basePoints * multiplier);

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        if (summary == null)
        {
            summary = new DrivingMonthlySummary
            {
                MemberId = memberId,
                Year = year,
                Month = month
            };
            _db.DrivingMonthlySummaries.Add(summary);
        }

        summary.TripCount = calc.TripCount;
        summary.TotalDistanceKm = calc.TotalKm;
        summary.AverageScore = calc.AverageScore;
        summary.BasePoints = basePoints;
        summary.MultiplierApplied = multiplier;
        summary.PointsAwarded = points;
        summary.IsFinalised = true;
        summary.FinalisedAt = DateTime.UtcNow;

        // Save first so the ledger entry can point at the summary's Id
        await _db.SaveChangesAsync();

        if (points > 0)
        {
            await _points.AwardAsync(member, points, multiplier, RewardCategory.Driving, "DrivingMonthlySummary", summary.DrivingMonthlySummaryId,
                string.Format("{0} driving in {1:MMMM yyyy}: average {2:N1} over {3:N0} km",
                    band!.Name, new DateTime(year, month, 1), calc.AverageScore, calc.TotalKm));

            await _db.SaveChangesAsync();

            var newTier = await _tiers.UpgradeIfQualifiedAsync(member);
            if (newTier != null)
            {
                await _db.SaveChangesAsync();
            }
        }

        await transaction.CommitAsync();
        return points;
    }

    /// <summary>Distance-weighted average: Σ(score × km) ÷ Σ km.</summary>
    private async Task<MonthCalculation> CalculateMonthAsync(int memberId, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var trips = await _db.DrivingTrips
            .Where(t => t.MemberId == memberId && t.StartedAt >= monthStart && t.StartedAt < monthEnd)
            .Select(t => new { t.DistanceKm, t.Score })
            .ToListAsync();

        decimal totalKm = trips.Sum(t => t.DistanceKm);
        decimal average = totalKm == 0
            ? 0m
            : Math.Round(trips.Sum(t => t.Score * t.DistanceKm) / totalKm, 1, MidpointRounding.AwayFromZero);

        return new MonthCalculation(trips.Count, totalKm, average);
    }

    private async Task<IReadOnlyList<DrivingRewardBand>> LoadBandsAsync()
    {
        // A handful of rows; sorted in memory because SQLite can't ORDER BY a decimal
        var bands = await _db.DrivingRewardBands.ToListAsync();
        return bands.OrderByDescending(b => b.MinScore).ToList();
    }

    /// <summary>Bands are sorted highest first, so the first one reached is the best one.</summary>
    private static DrivingRewardBand? PickBand(IReadOnlyList<DrivingRewardBand> bandsHighestFirst, decimal averageScore)
    {
        foreach (var band in bandsHighestFirst)
        {
            if (averageScore >= band.MinScore)
            {
                return band;
            }
        }
        return null;
    }

    private static void EnsureValidMonth(int month)
    {
        if (month < 1 || month > 12)
        {
            throw new BusinessRuleException("Month must be between 1 and 12.");
        }
    }
}
