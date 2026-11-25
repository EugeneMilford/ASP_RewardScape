using System.ComponentModel.DataAnnotations;
using SpendWell.Api.Models;

namespace SpendWell.Api.DTOs;

public record DealDto(
    int Id,
    string Title,
    string Description,
    int PointsCost,
    decimal CashPrice,
    string MinTier,
    int StockRemaining,
    int MaxPerMember,
    DateTime StartsAt,
    DateTime EndsAt);

/// <summary>A deal as one member sees it: their cash price after tier discount and whether they can redeem.</summary>
public record MemberDealDto(
    DealDto Deal,
    decimal YourCashPrice,
    bool CanRedeem,
    string? ReasonIfNot);

public record DealQueryDto(
    string? Tier,
    int? MaxPointsCost,
    bool OnlyAvailable = true);

public record CreateDealDto(
    [Required, StringLength(150)] string Title,
    [StringLength(1000)] string Description,
    [Range(1, 1_000_000)] int PointsCost,
    [Range(0, 100_000)] decimal CashPrice,
    [Required] int MinTierId,
    [Range(0, 1_000_000)] int StockRemaining,
    [Range(1, 100)] int MaxPerMember,
    [Range(1, 365)] int VoucherValidDays,
    DateTime StartsAt,
    DateTime EndsAt);

public record UpdateDealStockDto(
    [Range(0, 1_000_000)] int StockRemaining,
    bool IsActive);
