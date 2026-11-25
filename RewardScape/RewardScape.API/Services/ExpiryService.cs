using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IExpiryService
{
    Task<ExpiryRunResultDto> RunAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Expires points lots past their 12 months and vouchers past their valid date.
/// Unused voucher points are NOT refunded: an expired voucher is a spent voucher.
/// </summary>
public class ExpiryService : IExpiryService
{
    private readonly SpendWellDbContext _db;

    public ExpiryService(SpendWellDbContext db)
    {
        _db = db;
    }

    public async Task<ExpiryRunResultDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var lots = await _db.PointsLots
            .Where(l => !l.IsExpired && l.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        int pointsExpired = 0;

        foreach (var lot in lots)
        {
            if (lot.RemainingPoints > 0)
            {
                _db.PointsTransactions.Add(new PointsTransaction
                {
                    MemberId = lot.MemberId,
                    Type = PointsTransactionType.Expire,
                    Points = -lot.RemainingPoints,
                    SourceType = "PointsLot",
                    SourceId = lot.PointsLotId,
                    Description = string.Format("{0} points earned on {1:yyyy-MM-dd} expired",
                        lot.RemainingPoints, lot.EarnedAt),
                    CreatedAt = now
                });

                pointsExpired += lot.RemainingPoints;
                lot.RemainingPoints = 0;
            }

            lot.IsExpired = true;
        }

        var vouchers = await _db.Redemptions
            .Where(r => r.Status == RedemptionStatus.Issued && r.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var voucher in vouchers)
        {
            voucher.Status = RedemptionStatus.Expired;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ExpiryRunResultDto(lots.Count, pointsExpired, vouchers.Count);
    }
}

/// <summary>
/// Runs the scheduled reward jobs every 6 hours while the API is up:
/// 1. expire old points and vouchers,
/// 2. complete finished trips and pay their Travel points,
/// 3. close last month's driving and pay Driving points (from day 3 of the month).
/// Every job is safe to run repeatedly. Each gets its own DI scope (DbContext is scoped).
/// </summary>
public class RewardsBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RewardsBackgroundService> _logger;

    public RewardsBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RewardsBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await RunJobAsync("Points and voucher expiry", async services =>
            {
                var result = await services.GetRequiredService<IExpiryService>().RunAsync(stoppingToken);
                _logger.LogInformation("Expiry: {Lots} lots ({Points} points) and {Vouchers} vouchers expired",
                    result.LotsExpired, result.PointsExpired, result.VouchersExpired);
            });

            await RunJobAsync("Travel completion", async services =>
            {
                var result = await services.GetRequiredService<ITravelService>().CompleteFinishedTripsAsync(stoppingToken);
                _logger.LogInformation("Travel: {Bookings} bookings completed, {Points} points awarded",
                    result.BookingsCompleted, result.PointsAwarded);
            });

            await RunJobAsync("Driving month close", async services =>
            {
                var now = DateTime.UtcNow;
                if (now.Day <= DrivingRules.FinaliseGraceDays)
                {
                    return; // still inside the late-upload window
                }

                var lastMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
                var result = await services.GetRequiredService<IDrivingService>().FinaliseMonthAsync(lastMonth.Year, lastMonth.Month);
                _logger.LogInformation("Driving {Year}-{Month}: {Processed} members closed, {Rewarded} rewarded, {Points} points",
                    result.Year, result.Month, result.MembersProcessed, result.MembersRewarded, result.PointsAwarded);
            });
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunJobAsync(string name, Func<IServiceProvider, Task> job)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await job(scope.ServiceProvider);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "{Job} job failed", name);
        }
    }
}
