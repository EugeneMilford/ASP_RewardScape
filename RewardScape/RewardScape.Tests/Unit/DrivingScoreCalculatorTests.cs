using Shouldly;
using SpendWell.Api.Services;

namespace SpendWell.Tests.Unit;

public class DrivingScoreCalculatorTests
{
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    [Fact]
    public void Calculate_CleanTrip_Scores100()
    {
        var result = DrivingScoreCalculator.Calculate(60m, OneHour, 0, 0, 0, 0, 0, 0);

        result.Score.ShouldBe(100m);
    }

    [Fact]
    public void Calculate_OneHarshBrakeIn50Km_Costs4Points()
    {
        // 1 brake in 50 km = 2 per 100 km × weight 2.0
        var result = DrivingScoreCalculator.Calculate(50m, OneHour, 1, 0, 0, 0, 0, 0);

        result.BrakingPenalty.ShouldBe(4.0m);
        result.Score.ShouldBe(96.0m);
    }

    [Fact]
    public void Calculate_SameEventOnLongerTrip_CostsLess()
    {
        var shortTrip = DrivingScoreCalculator.Calculate(50m, OneHour, 1, 0, 0, 0, 0, 0);
        var longTrip = DrivingScoreCalculator.Calculate(200m, TimeSpan.FromHours(3), 1, 0, 0, 0, 0, 0);

        longTrip.Score.ShouldBeGreaterThan(shortTrip.Score);
    }

    [Fact]
    public void Calculate_PhoneUseFor10PercentOfTrip_Costs20Points()
    {
        // 360 s of 3 600 s = 10% × weight 2.0
        var result = DrivingScoreCalculator.Calculate(60m, OneHour, 0, 0, 0, 0, 360, 0);

        result.PhoneUsePenalty.ShouldBe(20.0m);
        result.Score.ShouldBe(80.0m);
    }

    [Fact]
    public void Calculate_AllAtNight_Costs20Points()
    {
        var result = DrivingScoreCalculator.Calculate(60m, OneHour, 0, 0, 0, 0, 0, 60);

        result.NightDrivingPenalty.ShouldBe(20.0m);
        result.Score.ShouldBe(80.0m);
    }

    [Fact]
    public void Calculate_SpeedingTheWholeTrip_NeverGoesBelowZero()
    {
        var result = DrivingScoreCalculator.Calculate(60m, OneHour, 5, 5, 5, 3600, 3600, 60);

        result.Score.ShouldBe(0m);
    }

    [Fact]
    public void Calculate_Breakdown_AddsUpToTheScore()
    {
        var result = DrivingScoreCalculator.Calculate(80m, OneHour, 1, 1, 1, 120, 60, 10);

        decimal penalties = result.BrakingPenalty + result.AccelerationPenalty + result.CorneringPenalty
            + result.SpeedingPenalty + result.PhoneUsePenalty + result.NightDrivingPenalty;

        result.Score.ShouldBe(100m - penalties, 0.1m);
    }
}
