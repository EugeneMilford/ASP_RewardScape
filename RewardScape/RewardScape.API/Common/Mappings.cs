using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Api.Common;

/// <summary>
/// Entity → DTO mapping. Done by hand (no AutoMapper) so every field is visible.
/// Callers must Include the navigation properties each method reads.
/// </summary>
public static class Mappings
{
    public static AccountSummaryDto ToSummaryDto(this Account account)
    {
        return new AccountSummaryDto(
            account.AccountId,
            account.AccountNumber,
            account.AccountType.ToString(),
            account.Status.ToString(),
            account.Balance);
    }

    public static AccountDto ToDto(this Account account)
    {
        return new AccountDto(
            account.AccountId,
            account.AccountNumber,
            account.AccountType.ToString(),
            account.Status.ToString(),
            account.Balance,
            account.OpenedAt,
            account.MemberId,
            account.Member.FullName);
    }

    public static AccountTransactionDto ToDto(this AccountTransaction transaction)
    {
        return new AccountTransactionDto(
            transaction.AccountTransactionId,
            transaction.CreatedAt,
            transaction.Type.ToString(),
            transaction.Direction.ToString(),
            transaction.Amount,
            transaction.BalanceAfter,
            transaction.Reference,
            transaction.Description,
            transaction.PurchaseId,
            transaction.RedemptionId);
    }

    public static MemberDto ToDto(this Member member)
    {
        return new MemberDto(
            member.MemberId,
            member.FullName,
            member.Email,
            member.PhoneNumber,
            member.Tier.Name,
            member.Tier.EarnMultiplier,
            member.JoinedAt,
            member.Accounts.Select(a => a.ToSummaryDto()).ToList());
    }

    public static PurchaseDto ToDto(this Purchase purchase)
    {
        return new PurchaseDto(
            purchase.PurchaseId,
            purchase.ExternalReference,
            purchase.MemberId,
            purchase.Account.AccountNumber,
            purchase.Store.Name,
            purchase.Amount,
            purchase.RefundedAmount,
            purchase.Status.ToString(),
            purchase.PurchasedAt,
            purchase.PointsEarned,
            purchase.Items.Select(i => new PurchaseItemDto(
                i.ProductName,
                i.Category.ToString(),
                i.Quantity,
                i.UnitPrice,
                i.LineTotal)).ToList());
    }

    public static PointsTransactionDto ToDto(this PointsTransaction transaction)
    {
        return new PointsTransactionDto(
            transaction.PointsTransactionId,
            transaction.CreatedAt,
            transaction.Type.ToString(),
            transaction.Points,
            transaction.MultiplierApplied,
            transaction.SourceType,
            transaction.SourceId,
            transaction.Description);
    }

    public static PointsLotDto ToDto(this PointsLot lot)
    {
        return new PointsLotDto(
            lot.PointsLotId,
            lot.OriginalPoints,
            lot.RemainingPoints,
            lot.EarnedAt,
            lot.ExpiresAt,
            lot.IsExpired);
    }

    public static DealDto ToDto(this Deal deal)
    {
        return new DealDto(
            deal.DealId,
            deal.Title,
            deal.Description,
            deal.PointsCost,
            deal.CashPrice,
            deal.MinTier.Name,
            deal.StockRemaining,
            deal.MaxPerMember,
            deal.StartsAt,
            deal.EndsAt);
    }

    public static RedemptionDto ToDto(this Redemption redemption)
    {
        return new RedemptionDto(
            redemption.RedemptionId,
            redemption.MemberId,
            redemption.DealId,
            redemption.Deal.Title,
            redemption.PointsSpent,
            redemption.CashPaid,
            redemption.VoucherCode,
            redemption.Status.ToString(),
            redemption.IssuedAt,
            redemption.ExpiresAt,
            redemption.UsedAt,
            redemption.CancelledAt);
    }

    public static TierDto ToDto(this Tier tier)
    {
        return new TierDto(
            tier.TierId,
            tier.Name,
            tier.Rank,
            tier.MinPointsRolling12M,
            tier.EarnMultiplier,
            tier.DealDiscountPercent,
            tier.TravelDiscountPercent);
    }

    public static MilestoneDto ToDto(this SpendMilestone milestone)
    {
        return new MilestoneDto(
            milestone.SpendMilestoneId,
            milestone.ThresholdAmount,
            milestone.CumulativePoints,
            milestone.IsActive);
    }

    public static StoreDto ToDto(this Store store)
    {
        return new StoreDto(store.StoreId, store.Name, store.Region, store.IsActive);
    }

    // ---------- Shopping ----------

    public static ProductDto ToDto(this Product product)
    {
        return new ProductDto(
            product.ProductId,
            product.Sku,
            product.Name,
            product.Description,
            product.Category.ToString(),
            product.Price,
            product.StockQuantity,
            product.IsActive && product.StockQuantity > 0,
            product.ImageUrl,
            product.IsActive);
    }

    /// <summary>Needs Account, Items and (optionally) Purchase loaded.</summary>
    public static OrderDto ToDto(this Order order)
    {
        return new OrderDto(
            order.OrderId,
            order.OrderNumber,
            order.MemberId,
            order.Account.AccountNumber,
            order.Status.ToString(),
            order.Subtotal,
            order.DeliveryFee,
            order.Total,
            new AddressDto(
                order.DeliveryAddress.Line1,
                order.DeliveryAddress.Line2,
                order.DeliveryAddress.Suburb,
                order.DeliveryAddress.City,
                order.DeliveryAddress.PostalCode),
            order.PlacedAt,
            order.StatusUpdatedAt,
            order.CancelledAt,
            order.PurchaseId,
            order.Purchase != null ? order.Purchase.PointsEarned : 0,
            order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.Sku,
                i.ProductName,
                i.Category.ToString(),
                i.Quantity,
                i.UnitPrice,
                i.LineTotal)).ToList());
    }

    // ---------- Travel ----------

    public static TravelProviderDto ToDto(this TravelProvider provider)
    {
        return new TravelProviderDto(provider.TravelProviderId, provider.Name, provider.Type.ToString(), provider.IsActive);
    }

    /// <summary>Needs Provider loaded.</summary>
    public static TravelListingDto ToDto(this TravelListing listing)
    {
        return new TravelListingDto(
            listing.TravelListingId,
            listing.ProviderId,
            listing.Provider.Name,
            listing.Type.ToString(),
            listing.Title,
            listing.Description,
            listing.Origin,
            listing.Destination,
            listing.AvailableFrom,
            listing.AvailableTo,
            listing.PricePerUnit,
            PriceUnitFor(listing.Type),
            listing.TotalUnits,
            listing.MaxUnitsPerBooking,
            listing.IsActive);
    }

    /// <summary>Needs Account, Listing.Provider and Travellers loaded.</summary>
    public static TravelBookingDto ToDto(this TravelBooking booking)
    {
        return new TravelBookingDto(
            booking.TravelBookingId,
            booking.BookingReference,
            booking.MemberId,
            booking.Account.AccountNumber,
            booking.ListingId,
            booking.Listing.Title,
            booking.Listing.Provider.Name,
            booking.Type.ToString(),
            booking.StartDate,
            booking.EndDate,
            booking.Units,
            booking.Nights,
            booking.BaseAmount,
            booking.TierDiscountPercent,
            booking.DiscountAmount,
            booking.TotalPaid,
            booking.CancellationFee,
            booking.RefundedAmount,
            booking.Status.ToString(),
            booking.PointsAwarded,
            booking.BookedAt,
            booking.CancelledAt,
            booking.CompletedAt,
            booking.Travellers.Select(t => new TravellerDto(t.FullName, t.IsChild)).ToList());
    }

    public static TravelEarnRuleDto ToDto(this TravelEarnRule rule)
    {
        return new TravelEarnRuleDto(rule.TravelEarnRuleId, rule.Type.ToString(), rule.PointsPerR10);
    }

    public static string PriceUnitFor(TravelType type)
    {
        switch (type)
        {
            case TravelType.Flight:
                return "per seat";
            case TravelType.Hotel:
                return "per room per night";
            case TravelType.CarHire:
                return "per car per day";
            default:
                return "per unit";
        }
    }

    // ---------- Driving ----------

    public static VehicleDto ToDto(this Vehicle vehicle)
    {
        return new VehicleDto(
            vehicle.VehicleId,
            vehicle.MemberId,
            vehicle.RegistrationNumber,
            vehicle.Make,
            vehicle.Model,
            vehicle.Year,
            vehicle.TelematicsDeviceId,
            vehicle.IsActive,
            vehicle.RegisteredAt);
    }

    /// <summary>Needs Vehicle loaded. The breakdown is recalculated from the stored events.</summary>
    public static DrivingTripDto ToDto(this DrivingTrip trip)
    {
        return new DrivingTripDto(
            trip.DrivingTripId,
            trip.ExternalTripId,
            trip.VehicleId,
            trip.Vehicle.RegistrationNumber,
            trip.StartedAt,
            trip.EndedAt,
            trip.DistanceKm,
            trip.Score,
            DrivingScoreCalculator.Calculate(trip));
    }

    public static DrivingRewardBandDto ToDto(this DrivingRewardBand band)
    {
        return new DrivingRewardBandDto(band.DrivingRewardBandId, band.Name, band.MinScore, band.BasePoints);
    }
}
