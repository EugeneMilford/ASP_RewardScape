using System.ComponentModel.DataAnnotations;
using SpendWell.Api.Models;

namespace SpendWell.Api.DTOs;

public record CreatePurchaseItemDto(
    [Required, StringLength(150)] string ProductName,
    ProductCategory Category,
    [Range(1, 1000)] int Quantity,
    [Range(0.01, 1_000_000)] decimal UnitPrice);

/// <summary>Sent by the till. The total is worked out from the items.</summary>
public record CreatePurchaseDto(
    [Required, StringLength(50)] string ExternalReference,
    [Required] int MemberId,
    [Required, StringLength(10, MinimumLength = 10)] string AccountNumber,
    [Required] int StoreId,
    [Required, MinLength(1)] List<CreatePurchaseItemDto> Items);

public record PurchaseItemDto(
    string ProductName,
    string Category,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record PurchaseDto(
    int Id,
    string ExternalReference,
    int MemberId,
    string AccountNumber,
    string StoreName,
    decimal Amount,
    decimal RefundedAmount,
    string Status,
    DateTime PurchasedAt,
    int PointsEarned,
    IReadOnlyList<PurchaseItemDto> Items);

/// <summary>What the till shows the customer after paying.</summary>
public record PurchaseResultDto(
    PurchaseDto Purchase,
    decimal AccountBalanceAfter,
    int PointsEarned,
    decimal MonthlySpendSoFar,
    decimal? AmountToNextMilestone,
    string? TierChangedTo);

public record CreateRefundDto(
    [Range(0.01, 1_000_000)] decimal Amount,
    [Required, StringLength(200)] string Reason);

public record RefundResultDto(
    int RefundId,
    int PurchaseId,
    decimal AmountRefunded,
    decimal AccountBalanceAfter,
    int PointsReversed,
    int PointsOwed,
    string PurchaseStatus);
