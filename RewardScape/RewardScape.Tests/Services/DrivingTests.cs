using Microsoft.EntityFrameworkCore;
using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Tests.TestHelpers;

namespace SpendWell.Tests.Services;

public class DrivingTests : DbTestBase
{
    private static UploadTripDto Trip(
        string deviceId, decimal km = 60m, int harshBrakes = 0, int phoneSeconds = 0,
        DateTime? startedAt = null, TimeSpan? duration = null, string? tripId = null)
    {
        var start = startedAt ?? DateTime.UtcNow.AddHours(-2);
        var end = start + (duration ?? TimeSpan.FromHours(1));

        return new UploadTripDto(
            tripId ?? Guid.NewGuid().ToString("N"),
            deviceId, start, end, km, harshBrakes, 0, 0, 0, phoneSeconds, 0);
    }

    // ---------- Vehicles ----------

    [Fact]
    public async Task RegisterVehicle_TidiesTheRegistration()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);

        var vehicle = await Services.Driving.RegisterVehicleAsync(
            new RegisterVehicleDto(member.MemberId, "ca 123-456", "Toyota", "Corolla", 2020, "DEV-ABC"));

        vehicle.RegistrationNumber.ShouldBe("CA123-456");
        vehicle.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task RegisterVehicle_SameRegistrationTwice_ThrowsConflict()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        await Services.Driving.RegisterVehicleAsync(new RegisterVehicleDto(member.MemberId, "CA111", "VW", "Polo", 2019, "DEV-1"));

        await Should.ThrowAsync<ConflictException>(() =>
            Services.Driving.RegisterVehicleAsync(new RegisterVehicleDto(member.MemberId, "CA 111", "VW", "Polo", 2019, "DEV-2")));
    }

    [Fact]
    public async Task RegisterVehicle_FourthActiveVehicle_IsRefused()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        for (int i = 1; i <= 3; i++)
        {
            await Services.Driving.RegisterVehicleAsync(new RegisterVehicleDto(
                member.MemberId, string.Format("CA{0}00", i), "VW", "Polo", 2019, string.Format("DEV-{0}", i)));
        }

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Driving.RegisterVehicleAsync(
            new RegisterVehicleDto(member.MemberId, "CA400", "VW", "Polo", 2019, "DEV-4")));
    }

    // ---------- Trips ----------

    [Fact]
    public async Task UploadTrip_CleanDrive_Scores100()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);

        var trip = await Services.Driving.UploadTripAsync(Trip(vehicle.TelematicsDeviceId));

        trip.Score.ShouldBe(100m);
        trip.RegistrationNumber.ShouldBe(vehicle.RegistrationNumber);
    }

    [Fact]
    public async Task UploadTrip_HarshBrake_LowersTheScore()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);

        var trip = await Services.Driving.UploadTripAsync(Trip(vehicle.TelematicsDeviceId, km: 50m, harshBrakes: 1));

        trip.Score.ShouldBe(96m);
        trip.Breakdown.BrakingPenalty.ShouldBe(4m);
    }

    [Fact]
    public async Task UploadTrip_SentTwice_ThrowsConflict_AndCountsOnce()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);
        var dto = Trip(vehicle.TelematicsDeviceId, tripId: "TRIP-ONCE");

        await Services.Driving.UploadTripAsync(dto);

        await Should.ThrowAsync<ConflictException>(() => Services.Driving.UploadTripAsync(dto));

        using var db = Fresh();
        (await db.DrivingTrips.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task UploadTrip_500KmInAnHour_IsRejected()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Driving.UploadTripAsync(Trip(vehicle.TelematicsDeviceId, km: 500m)));
    }

    [Fact]
    public async Task UploadTrip_PhoneUseLongerThanTheTrip_IsRejected()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Driving.UploadTripAsync(Trip(vehicle.TelematicsDeviceId, phoneSeconds: 4000)));
    }

    [Fact]
    public async Task UploadTrip_EndingInTheFuture_IsRejected()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Driving.UploadTripAsync(Trip(vehicle.TelematicsDeviceId, startedAt: DateTime.UtcNow)));
    }

    [Fact]
    public async Task UploadTrip_UnknownOrDeactivatedDevice_ThrowsNotFound()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);
        await Services.Driving.DeactivateVehicleAsync(vehicle.VehicleId);

        await Should.ThrowAsync<NotFoundException>(() => Services.Driving.UploadTripAsync(Trip("NO-SUCH-DEVICE")));
        await Should.ThrowAsync<NotFoundException>(() => Services.Driving.UploadTripAsync(Trip(vehicle.TelematicsDeviceId)));
    }

    // ---------- Monthly rewards ----------

    [Fact]
    public async Task MonthSummary_UsesADistanceWeightedAverage()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);
        var month = TestData.TwoMonthsAgo();
        await TestData.TripAsync(Db, vehicle, month.AddDays(3), 300m, 90m);
        await TestData.TripAsync(Db, vehicle, month.AddDays(5), 100m, 50m);

        var summary = await Services.Driving.GetMonthSummaryAsync(member.MemberId, month.Year, month.Month);

        // (300 × 90 + 100 × 50) ÷ 400 = 80
        summary.AverageScore.ShouldBe(80m);
        summary.TotalDistanceKm.ShouldBe(400m);
        summary.MeetsMinimumDistance.ShouldBeTrue();
        summary.CurrentBand.ShouldBe("Good");
        summary.Points.ShouldBe(400);
        summary.NextBand.ShouldBe("Excellent");
        summary.NextBandMinScore.ShouldBe(90m);
        summary.IsFinalised.ShouldBeFalse();
    }

    [Fact]
    public async Task MonthSummary_Under200Km_PaysNothing()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);
        var month = TestData.TwoMonthsAgo();
        await TestData.TripAsync(Db, vehicle, month.AddDays(3), 150m, 95m);

        var summary = await Services.Driving.GetMonthSummaryAsync(member.MemberId, month.Year, month.Month);

        summary.CurrentBand.ShouldBe("Excellent");
        summary.MeetsMinimumDistance.ShouldBeFalse();
        summary.Points.ShouldBe(0);
    }

    [Fact]
    public async Task FinaliseMonth_PaysDrivingPointsOnce()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);
        var month = TestData.TwoMonthsAgo();
        await TestData.TripAsync(Db, vehicle, month.AddDays(3), 300m, 90m);
        await TestData.TripAsync(Db, vehicle, month.AddDays(5), 100m, 50m);

        var first = await Services.Driving.FinaliseMonthAsync(month.Year, month.Month);
        var second = await Services.Driving.FinaliseMonthAsync(month.Year, month.Month);

        first.MembersRewarded.ShouldBe(1);
        first.PointsAwarded.ShouldBe(400);
        second.MembersProcessed.ShouldBe(0);
        second.PointsAwarded.ShouldBe(0);

        using var db = Fresh();
        var earn = await db.PointsTransactions.SingleAsync();
        earn.Category.ShouldBe(RewardCategory.Driving);
        earn.Points.ShouldBe(400);

        var summary = await Services.Driving.GetMonthSummaryAsync(member.MemberId, month.Year, month.Month);
        summary.IsFinalised.ShouldBeTrue();
        summary.Points.ShouldBe(400);
    }

    [Fact]
    public async Task FinaliseMonth_GoldMember_GetsTheMultiplier()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db, tierId: TestData.GoldTier);
        var vehicle = await TestData.VehicleAsync(Db, member);
        var month = TestData.TwoMonthsAgo();
        await TestData.TripAsync(Db, vehicle, month.AddDays(3), 250m, 95m);

        var result = await Services.Driving.FinaliseMonthAsync(month.Year, month.Month);

        result.PointsAwarded.ShouldBe(900);   // Excellent 600 × 1.5
    }

    [Fact]
    public async Task UploadTrip_IntoAClosedMonth_IsRefused()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var vehicle = await TestData.VehicleAsync(Db, member);
        var month = TestData.TwoMonthsAgo();
        await TestData.TripAsync(Db, vehicle, month.AddDays(3), 300m, 90m);
        await Services.Driving.FinaliseMonthAsync(month.Year, month.Month);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Driving.UploadTripAsync(Trip(vehicle.TelematicsDeviceId, startedAt: month.AddDays(10))));
    }

    [Fact]
    public async Task FinaliseMonth_CurrentMonth_IsTooEarly()
    {
        var now = DateTime.UtcNow;

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Driving.FinaliseMonthAsync(now.Year, now.Month));
    }
}
