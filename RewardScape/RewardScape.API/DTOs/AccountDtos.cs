using System.ComponentModel.DataAnnotations;
using SpendWell.Api.Models;

namespace SpendWell.Api.DTOs;

public record OpenAccountDto(
    [Required] int MemberId,
    AccountType AccountType,
    [Range(0, 1_000_000)] decimal OpeningDeposit);

public record AccountSummaryDto(
    int Id,
    string AccountNumber,
    string AccountType,
    string Status,
    decimal Balance);

public record AccountDto(
    int Id,
    string AccountNumber,
    string AccountType,
    string Status,
    decimal Balance,
    DateTime OpenedAt,
    int MemberId,
    string MemberName);

public record DepositDto(
    [Range(0.01, 1_000_000)] decimal Amount,
    [StringLength(100)] string? Description);

public record AccountTransactionDto(
    int Id,
    DateTime CreatedAt,
    string Type,
    string Direction,
    decimal Amount,
    decimal BalanceAfter,
    string Reference,
    string Description,
    int? PurchaseId,
    int? RedemptionId);

/// <summary>
/// Shows how much money went out of the account for a period and what it was spent on.
/// Purchases include online orders (store "SpendWell Online").
/// </summary>
public record SpendingSummaryDto(
    string AccountNumber,
    DateTime From,
    DateTime To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalMoneyIn,
    decimal TotalMoneyOut,
    decimal TotalSpentOnPurchases,
    decimal TotalRefunded,
    decimal NetShoppingSpend,
    decimal TotalSpentOnTravel,
    decimal TotalTravelRefunded,
    decimal NetTravelSpend,
    int TotalPointsEarned,
    IReadOnlyList<CategorySpendDto> ByCategory,
    IReadOnlyList<StoreSpendDto> ByStore,
    IReadOnlyList<SpendLineDto> Purchases,
    IReadOnlyList<TravelSpendLineDto> TravelBookings);

/// <summary>One travel booking on the summary: paid, refunded, and points once completed.</summary>
public record TravelSpendLineDto(
    string BookingReference,
    string Type,
    string Title,
    DateTime BookedAt,
    decimal AmountPaid,
    decimal AmountRefunded,
    decimal NetAmount,
    string Status,
    int PointsAwarded);

public record CategorySpendDto(
    string Category,
    decimal Amount,
    decimal PercentOfSpend);

public record StoreSpendDto(
    string StoreName,
    int PurchaseCount,
    decimal Amount);

/// <summary>One purchase on the summary: money out, money refunded, points earned.</summary>
public record SpendLineDto(
    int PurchaseId,
    DateTime PurchasedAt,
    string StoreName,
    string Reference,
    decimal AmountDebited,
    decimal AmountRefunded,
    decimal NetAmount,
    int PointsEarned,
    decimal BalanceAfter);
