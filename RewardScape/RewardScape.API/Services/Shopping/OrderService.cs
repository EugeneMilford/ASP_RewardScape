using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IOrderService
{
    Task<CheckoutResultDto> CheckoutAsync(int memberId, CheckoutDto dto);
    Task<OrderDto> GetAsync(int orderId);
    Task<PagedResult<OrderDto>> GetForMemberAsync(int memberId, int page, int pageSize);
    Task<OrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusDto dto);
    Task<OrderCancelResultDto> CancelAsync(int orderId, CancelOrderDto dto);
}

public class OrderService : IOrderService
{
    private readonly SpendWellDbContext _db;
    private readonly IPurchaseService _purchases;

    public OrderService(SpendWellDbContext db, IPurchaseService purchases)
    {
        _db = db;
        _purchases = purchases;
    }

    /// <summary>
    /// Turns the active cart into a paid order, in one transaction:
    /// 1. take stock, 2. create the order, 3. debit the account and earn milestone
    /// points (through the same purchase logic the tills use), 4. close the cart.
    /// If anything fails, nothing is saved.
    /// </summary>
    public async Task<CheckoutResultDto> CheckoutAsync(int memberId, CheckoutDto dto)
    {
        var member = await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == memberId)
            ?? throw NotFoundException.For("Member", memberId);

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == dto.AccountNumber)
            ?? throw NotFoundException.For("Account", dto.AccountNumber);

        if (account.MemberId != member.MemberId)
        {
            throw new BusinessRuleException("This account does not belong to the member.");
        }

        AccountPosting.EnsureUsable(account);

        var cart = await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.Status == CartStatus.Active);

        if (cart == null || cart.Items.Count == 0)
        {
            throw new BusinessRuleException("Your cart is empty.");
        }

        // Every problem at once, so the member can fix the cart in one go
        var problems = new List<string>();
        foreach (var line in cart.Items)
        {
            if (!line.Product.IsActive)
            {
                problems.Add(string.Format("{0} is no longer sold.", line.Product.Name));
            }
            else if (line.Product.StockQuantity < line.Quantity)
            {
                problems.Add(string.Format("Only {0} of {1} left.", line.Product.StockQuantity, line.Product.Name));
            }
        }

        if (problems.Count > 0)
        {
            throw new BusinessRuleException(string.Join(" ", problems));
        }

        var orderItems = cart.Items.Select(line => new OrderItem
        {
            ProductId = line.ProductId,
            ProductName = line.Product.Name,
            Sku = line.Product.Sku,
            Category = line.Product.Category,
            Quantity = line.Quantity,
            UnitPrice = line.Product.Price,
            LineTotal = Math.Round(line.Product.Price * line.Quantity, 2)
        }).ToList();

        decimal subtotal = orderItems.Sum(i => i.LineTotal);
        decimal deliveryFee = ShoppingRules.DeliveryFeeFor(subtotal);
        decimal total = subtotal + deliveryFee;

        if (account.Balance < total)
        {
            throw new BusinessRuleException(string.Format(
                "Insufficient funds. The order is R{0:N2} and account {1} has R{2:N2}.",
                total, account.AccountNumber, account.Balance));
        }

        var store = await _db.Stores.FirstOrDefaultAsync(s => s.StoreId == ShoppingRules.OnlineStoreId)
            ?? throw NotFoundException.For("Store", ShoppingRules.OnlineStoreId);

        var now = DateTime.UtcNow;

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        // 1. Take stock. Product.RowVersion makes a clash with another checkout fail with 409.
        foreach (var line in cart.Items)
        {
            line.Product.StockQuantity -= line.Quantity;
        }

        // 2. The order
        var order = new Order
        {
            OrderNumber = await GenerateOrderNumberAsync(now),
            Member = member,
            Account = account,
            Cart = cart,
            Subtotal = subtotal,
            DeliveryFee = deliveryFee,
            Total = total,
            Status = OrderStatus.Paid,
            DeliveryAddress = new Address
            {
                Line1 = dto.DeliveryAddress.Line1.Trim(),
                Line2 = dto.DeliveryAddress.Line2,
                Suburb = dto.DeliveryAddress.Suburb.Trim(),
                City = dto.DeliveryAddress.City.Trim(),
                PostalCode = dto.DeliveryAddress.PostalCode
            },
            PlacedAt = now,
            StatusUpdatedAt = now,
            Items = orderItems
        };
        _db.Orders.Add(order);

        // 3. Close the cart (Cart.RowVersion stops a double-submitted checkout)
        cart.Status = CartStatus.CheckedOut;
        cart.CheckedOutAt = now;
        cart.UpdatedAt = now;

        // 4. Pay and earn: the delivery fee is a line too, so the debit matches the order total
        var purchaseItems = orderItems.Select(i => new PurchaseItem
        {
            ProductName = i.ProductName,
            Category = i.Category,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineTotal = i.LineTotal
        }).ToList();

        if (deliveryFee > 0)
        {
            purchaseItems.Add(new PurchaseItem
            {
                ProductName = "Delivery",
                Category = ProductCategory.Delivery,
                Quantity = 1,
                UnitPrice = deliveryFee,
                LineTotal = deliveryFee
            });
        }

        var result = await _purchases.RecordPurchaseAsync(member, account, store, purchaseItems,
            order.OrderNumber, string.Format("Online order {0}", order.OrderNumber));

        order.Purchase = result.Purchase;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new CheckoutResultDto(
            order.ToDto(),
            account.Balance,
            result.PointsAwarded,
            result.MonthlySpend,
            result.AmountToNextMilestone,
            result.TierChangedTo);
    }

    public async Task<OrderDto> GetAsync(int orderId)
    {
        var order = await LoadOrderQuery().FirstOrDefaultAsync(o => o.OrderId == orderId)
            ?? throw NotFoundException.For("Order", orderId);

        return order.ToDto();
    }

    public async Task<PagedResult<OrderDto>> GetForMemberAsync(int memberId, int page, int pageSize)
    {
        bool exists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!exists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = LoadOrderQuery().Where(o => o.MemberId == memberId);
        int total = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.PlacedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<OrderDto>(orders.Select(o => o.ToDto()).ToList(), page, pageSize, total);
    }

    /// <summary>Fulfilment moves one step at a time: Paid → Packed → Shipped → Delivered.</summary>
    public async Task<OrderDto> UpdateStatusAsync(int orderId, UpdateOrderStatusDto dto)
    {
        var order = await LoadOrderQuery().FirstOrDefaultAsync(o => o.OrderId == orderId)
            ?? throw NotFoundException.For("Order", orderId);

        if (dto.Status == OrderStatus.Cancelled)
        {
            throw new BusinessRuleException("Use the cancel endpoint to cancel an order, so it is refunded and restocked.");
        }

        OrderStatus? allowedNext;
        switch (order.Status)
        {
            case OrderStatus.Paid:
                allowedNext = OrderStatus.Packed;
                break;
            case OrderStatus.Packed:
                allowedNext = OrderStatus.Shipped;
                break;
            case OrderStatus.Shipped:
                allowedNext = OrderStatus.Delivered;
                break;
            default:
                allowedNext = null;
                break;
        }

        if (allowedNext == null || dto.Status != allowedNext.Value)
        {
            throw new BusinessRuleException(string.Format(
                "An order that is {0} can't move to {1}.", order.Status, dto.Status));
        }

        order.Status = dto.Status;
        order.StatusUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return order.ToDto();
    }

    /// <summary>
    /// Cancels an order that hasn't shipped: stock goes back on the shelf, the full amount
    /// is refunded to the account, and milestone points are clawed back if the month drops.
    /// </summary>
    public async Task<OrderCancelResultDto> CancelAsync(int orderId, CancelOrderDto dto)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Account)
            .Include(o => o.Purchase).ThenInclude(p => p!.Member)
            .Include(o => o.Purchase).ThenInclude(p => p!.Account)
            .FirstOrDefaultAsync(o => o.OrderId == orderId)
            ?? throw NotFoundException.For("Order", orderId);

        switch (order.Status)
        {
            case OrderStatus.Paid:
            case OrderStatus.Packed:
                break;
            case OrderStatus.Shipped:
            case OrderStatus.Delivered:
                throw new BusinessRuleException(
                    "This order has already shipped. Return the items and refund them on the order's purchase instead.");
            case OrderStatus.Cancelled:
                throw new BusinessRuleException("This order is already cancelled.");
        }

        if (order.Purchase == null)
        {
            throw new BusinessRuleException("This order has no payment linked to it and can't be refunded automatically.");
        }

        var productIds = order.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.ProductId)).ToListAsync();

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        // 1. Back on the shelf
        foreach (var item in order.Items)
        {
            var product = products.First(p => p.ProductId == item.ProductId);
            product.StockQuantity += item.Quantity;
        }

        // 2. Money back (and points clawed back if needed)
        decimal toRefund = order.Purchase.Amount - order.Purchase.RefundedAmount;
        int pointsReversed = 0;

        if (toRefund > 0)
        {
            var refund = await _purchases.RefundPurchaseAsync(order.Purchase, toRefund,
                string.Format("Order {0} cancelled: {1}", order.OrderNumber, dto.Reason));
            pointsReversed = refund.PointsReversed;
        }

        // 3. Close the order
        var now = DateTime.UtcNow;
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = now;
        order.StatusUpdatedAt = now;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new OrderCancelResultDto(order.ToDto(), toRefund, order.Account.Balance, pointsReversed);
    }

    private IQueryable<Order> LoadOrderQuery()
    {
        return _db.Orders
            .Include(o => o.Items)
            .Include(o => o.Account)
            .Include(o => o.Purchase);
    }

    /// <summary>ORD-yyMMdd-XXXXXX, checked for clashes.</summary>
    private async Task<string> GenerateOrderNumberAsync(DateTime now)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string candidate = string.Format("ORD-{0:yyMMdd}-{1}", now, CodeGenerator.Random(6));

            bool taken = await _db.Orders.AnyAsync(o => o.OrderNumber == candidate);
            if (!taken)
            {
                return candidate;
            }
        }

        throw new ConflictException("Could not generate an order number. Please try again.");
    }
}
