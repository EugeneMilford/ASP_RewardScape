using Microsoft.EntityFrameworkCore;
using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Tests.TestHelpers;

namespace SpendWell.Tests.Services;

public class TravelTests : DbTestBase
{
    private static DateTime Day(int daysFromToday)
    {
        return DateTime.UtcNow.Date.AddDays(daysFromToday);
    }

    private Task<TravelBookingResultDto> BookHotelAsync(Member member, Account account, TravelListing hotel, int fromDay, int toDay)
    {
        return Services.Travel.BookAsync(new CreateTravelBookingDto(
            member.MemberId, account.AccountNumber, hotel.TravelListingId, Day(fromDay), Day(toDay), 1, TestData.Adults(1)));
    }

    [Fact]
    public async Task Quote_Hotel_TwoNights_Blue()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var hotel = await TestData.HotelAsync(Db, pricePerNight: 1200m);

        var quote = await Services.Travel.QuoteAsync(new TravelQuoteRequestDto(member.MemberId, hotel.TravelListingId, Day(10), Day(12), 1));

        quote.Nights.ShouldBe(2);
        quote.BaseAmount.ShouldBe(2400m);
        quote.DiscountAmount.ShouldBe(0m);
        quote.TotalToPay.ShouldBe(2400m);
        quote.EstimatedPoints.ShouldBe(720);
        quote.FreeCancellationUntil.ShouldBe(Day(10).AddHours(-48));
    }

    [Fact]
    public async Task Quote_Hotel_Gold_Gets15PercentOff_AndMorePoints()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db, tierId: TestData.GoldTier);
        var hotel = await TestData.HotelAsync(Db, pricePerNight: 1200m);

        var quote = await Services.Travel.QuoteAsync(new TravelQuoteRequestDto(member.MemberId, hotel.TravelListingId, Day(10), Day(12), 1));

        quote.DiscountAmount.ShouldBe(360m);
        quote.TotalToPay.ShouldBe(2040m);
        quote.EstimatedPoints.ShouldBe(918);   // 204 × 3 = 612 × 1.5
    }

    [Fact]
    public async Task Book_PaysNow_PointsOnlyLater()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 5000m);
        var hotel = await TestData.HotelAsync(Db, pricePerNight: 1200m);

        var result = await BookHotelAsync(member, account, hotel, 10, 12);

        result.AccountBalanceAfter.ShouldBe(2600m);
        result.EstimatedPoints.ShouldBe(720);
        result.Booking.Status.ShouldBe("Confirmed");
        result.Booking.PointsAwarded.ShouldBe(0);
        result.Booking.BookingReference.ShouldStartWith("TRV-");

        using var db = Fresh();
        (await db.PointsTransactions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Book_LastRoom_OverlappingStayIsRefused_BackToBackIsFine()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 20_000m);
        var hotel = await TestData.HotelAsync(Db, rooms: 1);
        await BookHotelAsync(member, account, hotel, 10, 12);

        // Overlaps the night of day 11
        await Should.ThrowAsync<BusinessRuleException>(() => BookHotelAsync(member, account, hotel, 11, 13));

        // Checks in the day the first guest checks out
        var backToBack = await BookHotelAsync(member, account, hotel, 12, 14);
        backToBack.Booking.Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task Availability_CountsTheBusiestNight()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 20_000m);
        var hotel = await TestData.HotelAsync(Db, rooms: 3);
        await BookHotelAsync(member, account, hotel, 10, 12);
        await BookHotelAsync(member, account, hotel, 11, 13);

        var availability = await Services.Travel.GetAvailabilityAsync(hotel.TravelListingId, Day(10), Day(13));

        availability.UnitsAvailable.ShouldBe(1);   // night 11 has 2 of 3 rooms taken
    }

    [Fact]
    public async Task Book_Flight_NeedsOneTravellerPerSeat()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var flight = await TestData.FlightAsync(Db);

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Travel.BookAsync(new CreateTravelBookingDto(
            member.MemberId, account.AccountNumber, flight.TravelListingId, null, null, 2, TestData.Adults(1))));
    }

    [Fact]
    public async Task Book_Flight_UsesTheFlightTimes_AndChargesPerSeat()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 10_000m);
        var flight = await TestData.FlightAsync(Db, pricePerSeat: 1450m);

        var result = await Services.Travel.BookAsync(new CreateTravelBookingDto(
            member.MemberId, account.AccountNumber, flight.TravelListingId, null, null, 2, TestData.Adults(2)));

        result.Booking.TotalPaid.ShouldBe(2900m);
        result.Booking.StartDate.ShouldBe(flight.AvailableFrom);
    }

    [Fact]
    public async Task Book_OnlyChildren_IsRefused()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var hotel = await TestData.HotelAsync(Db);

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Travel.BookAsync(new CreateTravelBookingDto(
            member.MemberId, account.AccountNumber, hotel.TravelListingId, Day(10), Day(12), 1,
            new List<TravellerDto> { new TravellerDto("Child", true) })));
    }

    [Fact]
    public async Task Book_Hotel_WithoutDates_IsRefused()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var hotel = await TestData.HotelAsync(Db);

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Travel.BookAsync(new CreateTravelBookingDto(
            member.MemberId, account.AccountNumber, hotel.TravelListingId, null, null, 1, TestData.Adults(1))));
    }

    [Fact]
    public async Task Book_OutsideTheListingWindow_IsRefused()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var hotel = await TestData.HotelAsync(Db);

        await Should.ThrowAsync<BusinessRuleException>(() => BookHotelAsync(member, account, hotel, 400, 402));
    }

    [Fact]
    public async Task Book_NotEnoughMoney_Throws_AndSavesNothing()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 100m);
        var hotel = await TestData.HotelAsync(Db);

        await Should.ThrowAsync<BusinessRuleException>(() => BookHotelAsync(member, account, hotel, 10, 12));

        using var db = Fresh();
        (await db.TravelBookings.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Cancel_MoreThan48HoursAhead_FullRefund_AndTheRoomFreesUp()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 5000m);
        var hotel = await TestData.HotelAsync(Db, pricePerNight: 1200m, rooms: 1);
        var booking = await BookHotelAsync(member, account, hotel, 10, 12);

        var result = await Services.Travel.CancelAsync(booking.Booking.Id);

        result.CancellationFee.ShouldBe(0m);
        result.AmountRefunded.ShouldBe(2400m);
        result.AccountBalanceAfter.ShouldBe(5000m);

        var rebooked = await BookHotelAsync(member, account, hotel, 10, 12);
        rebooked.Booking.Status.ShouldBe("Confirmed");
    }

    [Fact]
    public async Task Cancel_Within48Hours_KeepsHalf()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 5000m);
        var hotel = await TestData.HotelAsync(Db, pricePerNight: 1200m);
        var booking = await BookHotelAsync(member, account, hotel, 1, 2);   // starts tomorrow at midnight

        var result = await Services.Travel.CancelAsync(booking.Booking.Id);

        result.CancellationFee.ShouldBe(600m);
        result.AmountRefunded.ShouldBe(600m);
        result.AccountBalanceAfter.ShouldBe(4400m);
    }

    [Fact]
    public async Task Cancel_Twice_Throws()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 5000m);
        var hotel = await TestData.HotelAsync(Db);
        var booking = await BookHotelAsync(member, account, hotel, 10, 12);
        await Services.Travel.CancelAsync(booking.Booking.Id);

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Travel.CancelAsync(booking.Booking.Id));
    }

    [Fact]
    public async Task Complete_FinishedTrip_PaysTravelPoints()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var hotel = await TestData.HotelAsync(Db);
        var booking = await TestData.BookingAsync(Db, member, account, hotel, Day(-5), Day(-3), 2400m);

        var result = await Services.Travel.CompleteAsync(booking.TravelBookingId);

        result.Status.ShouldBe("Completed");
        result.PointsAwarded.ShouldBe(720);

        using var db = Fresh();
        var earn = await db.PointsTransactions.SingleAsync();
        earn.Category.ShouldBe(RewardCategory.Travel);
        earn.Points.ShouldBe(720);
    }

    [Fact]
    public async Task Complete_TripNotFinished_Throws()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 5000m);
        var hotel = await TestData.HotelAsync(Db);
        var booking = await BookHotelAsync(member, account, hotel, 10, 12);

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Travel.CompleteAsync(booking.Booking.Id));
    }

    [Fact]
    public async Task Complete_Twice_Throws()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var hotel = await TestData.HotelAsync(Db);
        var booking = await TestData.BookingAsync(Db, member, account, hotel, Day(-5), Day(-3), 2400m);
        await Services.Travel.CompleteAsync(booking.TravelBookingId);

        await Should.ThrowAsync<BusinessRuleException>(() => Services.Travel.CompleteAsync(booking.TravelBookingId));
    }

    [Fact]
    public async Task CompleteFinishedTrips_OnlyCompletesTripsThatHaveEnded()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);
        var hotel = await TestData.HotelAsync(Db);
        await TestData.BookingAsync(Db, member, account, hotel, Day(-5), Day(-3), 2400m);
        await TestData.BookingAsync(Db, member, account, hotel, Day(20), Day(22), 2400m);

        var result = await Services.Travel.CompleteFinishedTripsAsync();

        result.BookingsCompleted.ShouldBe(1);
        result.PointsAwarded.ShouldBe(720);
    }
}