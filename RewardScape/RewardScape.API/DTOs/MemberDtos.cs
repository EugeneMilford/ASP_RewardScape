using System.ComponentModel.DataAnnotations;

namespace SpendWell.Api.DTOs;

public record CreateMemberDto(
    [Required, StringLength(100)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, Phone] string PhoneNumber,
    [Range(0, 1_000_000)] decimal OpeningDeposit);

public record MemberDto(
    int Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string Tier,
    decimal EarnMultiplier,
    DateTime JoinedAt,
    IReadOnlyList<AccountSummaryDto> Accounts);

public record PointsBalanceDto(
    int MemberId,
    int AvailablePoints,
    int PointsOwed,
    int SpendablePoints,
    int PointsExpiringNext30Days,
    DateTime? NextExpiryDate);

/// <summary>
/// "You've spent R1 150 this month, R1 350 to go until the next milestone."
/// </summary>
public record MonthlyProgressDto(
    int MemberId,
    int Year,
    int Month,
    decimal TotalSpend,
    int PointsEarnedThisMonth,
    string CurrentTier,
    decimal EarnMultiplier,
    decimal? NextMilestoneThreshold,
    decimal? AmountToNextMilestone,
    int? PointsAtNextMilestone,
    IReadOnlyList<MilestoneProgressDto> Milestones);

public record MilestoneProgressDto(
    decimal ThresholdAmount,
    int CumulativePoints,
    bool Reached);

/// <summary>
/// One view of everything the member earns from: shopping, travel, driving and bonuses,
/// plus how close they are to the next tier.
/// </summary>
public record RewardsSummaryDto(
    int MemberId,
    string Tier,
    decimal EarnMultiplier,
    decimal TravelDiscountPercent,
    int Year,
    int SpendablePoints,
    int TotalEarnedThisYear,
    IReadOnlyList<CategoryPointsDto> ByCategory,
    int PointsEarnedRolling12M,
    string? NextTier,
    int? PointsToNextTier);

public record CategoryPointsDto(
    string Category,
    int PointsThisMonth,
    int PointsThisYear);

public record TierHistoryDto(
    string FromTier,
    string ToTier,
    DateTime ChangedAt,
    string Reason);
