using System.ComponentModel.DataAnnotations;
using SpendWell.Api.Models;

namespace SpendWell.Api.DTOs;

public record MilestoneDto(
    int Id,
    decimal ThresholdAmount,
    int CumulativePoints,
    bool IsActive);

public record CreateMilestoneDto(
    [Range(1, 10_000_000)] decimal ThresholdAmount,
    [Range(1, 10_000_000)] int CumulativePoints);

public record TierDto(
    int Id,
    string Name,
    int Rank,
    int MinPointsRolling12M,
    decimal EarnMultiplier,
    decimal DealDiscountPercent,
    decimal TravelDiscountPercent);

public record TierReviewResultDto(
    int MembersReviewed,
    int Upgraded,
    int Downgraded,
    int Unchanged);

public record StoreDto(
    int Id,
    string Name,
    string Region,
    bool IsActive);

public record CreateStoreDto(
    [Required, StringLength(100)] string Name,
    [Required, StringLength(100)] string Region);

public record ExpiryRunResultDto(
    int LotsExpired,
    int PointsExpired,
    int VouchersExpired);
