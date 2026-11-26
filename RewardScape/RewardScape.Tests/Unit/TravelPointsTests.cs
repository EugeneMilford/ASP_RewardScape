using Shouldly;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Tests.Unit;

/// <summary>Every full R10 × the type's rate × the tier multiplier, rounded down.</summary>
public class TravelPointsTests
{
    private static readonly TravelEarnRule Hotel = new TravelEarnRule { Type = TravelType.Hotel, PointsPerR10 = 3.0m };
    private static readonly TravelEarnRule Flight = new TravelEarnRule { Type = TravelType.Flight, PointsPerR10 = 2.0m };

    [Theory]
    [InlineData(2400, 1.0, 720)]
    [InlineData(2890, 1.5, 1300)]
    [InlineData(2890, 2.0, 1734)]
    public void CalculatePoints_Hotel(decimal amount, decimal multiplier, int expected)
    {
        TravelService.CalculatePoints(amount, Hotel, multiplier).ShouldBe(expected);
    }

    [Fact]
    public void CalculatePoints_Flight_UsesTheFlightRate()
    {
        // 145 × 2 = 290
        TravelService.CalculatePoints(1450m, Flight, 1.0m).ShouldBe(290);
    }

    [Theory]
    [InlineData(9.99)]
    [InlineData(0)]
    [InlineData(-50)]
    public void CalculatePoints_LessThanR10OrNothing_EarnsNothing(decimal amount)
    {
        TravelService.CalculatePoints(amount, Hotel, 2.0m).ShouldBe(0);
    }

    [Fact]
    public void CalculatePoints_OnlyFullR10sCount()
    {
        // R19.99 is one full R10
        TravelService.CalculatePoints(19.99m, Hotel, 1.0m).ShouldBe(3);
    }
}
