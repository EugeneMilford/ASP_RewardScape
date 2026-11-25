using System.ComponentModel.DataAnnotations;
using SpendWell.Api.Models;

namespace SpendWell.Api.DTOs;

// ---------- Products ----------

public record ProductDto(
    int Id,
    string Sku,
    string Name,
    string Description,
    string Category,
    decimal Price,
    int StockQuantity,
    bool InStock,
    string? ImageUrl,
    bool IsActive);

public record ProductQueryDto(
    string? Search,
    ProductCategory? Category,
    decimal? MinPrice,
    decimal? MaxPrice,
    bool InStockOnly = false,
    int Page = 1,
    int PageSize = 20);

public record CreateProductDto(
    [Required, StringLength(30)] string Sku,
    [Required, StringLength(150)] string Name,
    [StringLength(1000)] string Description,
    ProductCategory Category,
    [Range(0.01, 1_000_000)] decimal Price,
    [Range(0, 1_000_000)] int StockQuantity,
    [StringLength(500)] string? ImageUrl);

public record UpdateProductDto(
    [Required, StringLength(150)] string Name,
    [StringLength(1000)] string Description,
    ProductCategory Category,
    [Range(0.01, 1_000_000)] decimal Price,
    [StringLength(500)] string? ImageUrl,
    bool IsActive);

public record RestockDto(
    [Range(1, 1_000_000)] int Quantity);

// ---------- Cart ----------

public record AddCartItemDto(
    [Required] int ProductId,
    [Range(1, 20)] int Quantity);

public record UpdateCartItemDto(
    [Range(1, 20)] int Quantity);

public record CartItemDto(
    int ProductId,
    string Sku,
    string ProductName,
    string Category,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool Available,
    int StockAvailable);

/// <summary>
/// The cart with live prices. Warnings explain anything blocking checkout
/// (out of stock, product withdrawn).
/// </summary>
public record CartDto(
    int CartId,
    int MemberId,
    string Status,
    IReadOnlyList<CartItemDto> Items,
    int ItemCount,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    decimal AmountToFreeDelivery,
    bool CanCheckout,
    IReadOnlyList<string> Warnings);

// ---------- Checkout & orders ----------

public record AddressDto(
    [Required, StringLength(150)] string Line1,
    [StringLength(150)] string? Line2,
    [Required, StringLength(100)] string Suburb,
    [Required, StringLength(100)] string City,
    [Required, RegularExpression(@"^\d{4}$", ErrorMessage = "Postal code must be 4 digits.")] string PostalCode);

public record CheckoutDto(
    [Required, StringLength(10, MinimumLength = 10)] string AccountNumber,
    [Required] AddressDto DeliveryAddress);

public record OrderItemDto(
    int ProductId,
    string Sku,
    string ProductName,
    string Category,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record OrderDto(
    int Id,
    string OrderNumber,
    int MemberId,
    string AccountNumber,
    string Status,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    AddressDto DeliveryAddress,
    DateTime PlacedAt,
    DateTime StatusUpdatedAt,
    DateTime? CancelledAt,
    int? PurchaseId,
    int PointsEarned,
    IReadOnlyList<OrderItemDto> Items);

/// <summary>What the checkout screen shows after paying.</summary>
public record CheckoutResultDto(
    OrderDto Order,
    decimal AccountBalanceAfter,
    int PointsEarned,
    decimal MonthlySpendSoFar,
    decimal? AmountToNextMilestone,
    string? TierChangedTo);

public record UpdateOrderStatusDto(
    OrderStatus Status);

public record CancelOrderDto(
    [Required, StringLength(200)] string Reason);

public record OrderCancelResultDto(
    OrderDto Order,
    decimal AmountRefunded,
    decimal AccountBalanceAfter,
    int PointsReversed);
