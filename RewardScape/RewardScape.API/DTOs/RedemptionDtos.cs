using System.ComponentModel.DataAnnotations;

namespace SpendWell.Api.DTOs;

/// <summary>AccountNumber is only needed when the deal has a cash price.</summary>
public record RedeemDealDto(
    [Required] int MemberId,
    [StringLength(10, MinimumLength = 10)] string? AccountNumber);

public record RedemptionDto(
    int Id,
    int MemberId,
    int DealId,
    string DealTitle,
    int PointsSpent,
    decimal CashPaid,
    string VoucherCode,
    string Status,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    DateTime? UsedAt,
    DateTime? CancelledAt);

public record RedemptionResultDto(
    RedemptionDto Redemption,
    int PointsBalanceAfter,
    decimal? AccountBalanceAfter,
    IReadOnlyList<LotUsageDto> LotsUsed);

public record LotUsageDto(
    int PointsLotId,
    int PointsTaken,
    DateTime LotExpiresAt);

/// <summary>Sent by the till when a voucher is scanned. StoreId is the store accepting it.</summary>
public record UseVoucherDto(
    [Required] int StoreId);
