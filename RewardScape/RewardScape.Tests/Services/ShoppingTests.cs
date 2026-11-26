using Microsoft.EntityFrameworkCore;
using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Tests.TestHelpers;

namespace SpendWell.Tests.Services;

public class CartTests : DbTestBase
{
    [Fact]
    public async Task GetCart_FirstTime_GivesAnEmptyCart()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);

        var cart = await Services.Carts.GetCartAsync(member.MemberId);

        cart.Items.ShouldBeEmpty();
        cart.Total.ShouldBe(0m);
        cart.CanCheckout.ShouldBeFalse();
        cart.Status.ShouldBe("Active");
    }

    [Fact]
    public async Task AddItem_Twice_IncreasesTheQuantity()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var product = await TestData.ProductAsync(Db, 30m);

        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 2));
        var cart = await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 3));

        cart.Items.Count.ShouldBe(1);
        cart.Items[0].Quantity.ShouldBe(5);
        cart.Subtotal.ShouldBe(150m);
    }

    [Fact]
    public async Task AddItem_MoreThanInStock_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var product = await TestData.ProductAsync(Db, 30m, stock: 3);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 4)));
    }

    [Fact]
    public async Task AddItem_MoreThan20OfOneProduct_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var product = await TestData.ProductAsync(Db, 10m, stock: 100);
        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 15));

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 10)));
    }

    [Fact]
    public async Task AddItem_WithdrawnProduct_Throws()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var product = await TestData.ProductAsync(Db, 10m);
        product.IsActive = false;
        await Db.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 1)));
    }

    [Fact]
    public async Task Cart_UnderR500_ChargesDelivery_AndShowsTheGap()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var product = await TestData.ProductAsync(Db, 120m);

        var cart = await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 2));

        cart.Subtotal.ShouldBe(240m);
        cart.DeliveryFee.ShouldBe(60m);
        cart.Total.ShouldBe(300m);
        cart.AmountToFreeDelivery.ShouldBe(260m);
        cart.CanCheckout.ShouldBeTrue();
    }

    [Fact]
    public async Task Cart_ProductWithdrawnAfterAdding_WarnsAndBlocksCheckout()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var product = await TestData.ProductAsync(Db, 50m);
        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 1));

        product.IsActive = false;
        await Db.SaveChangesAsync();
        var cart = await Services.Carts.GetCartAsync(member.MemberId);

        cart.Warnings.Count.ShouldBe(1);
        cart.Items[0].Available.ShouldBeFalse();
        cart.CanCheckout.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateItem_SetsTheQuantity()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var product = await TestData.ProductAsync(Db, 10m);
        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 5));

        var cart = await Services.Carts.UpdateItemAsync(member.MemberId, product.ProductId, new UpdateCartItemDto(2));

        cart.Items[0].Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveItem_And_Clear_EmptyTheCart()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);
        var first = await TestData.ProductAsync(Db, 10m);
        var second = await TestData.ProductAsync(Db, 20m);
        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(first.ProductId, 1));
        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(second.ProductId, 1));

        var afterRemove = await Services.Carts.RemoveItemAsync(member.MemberId, first.ProductId);
        var afterClear = await Services.Carts.ClearAsync(member.MemberId);

        afterRemove.Items.Single().ProductId.ShouldBe(second.ProductId);
        afterClear.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveItem_NotInCart_ThrowsNotFound()
    {
        var (member, _) = await TestData.MemberWithAccountAsync(Db);

        await Should.ThrowAsync<NotFoundException>(() => Services.Carts.RemoveItemAsync(member.MemberId, 12345));
    }
}

public class OrderTests : DbTestBase
{
    private async Task<(Member Member, Account Account, Product Earbuds, Product Kettle)> CartWithR948Async(decimal balance = 10_000m)
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance);
        var earbuds = await TestData.ProductAsync(Db, 599m, stock: 5, category: ProductCategory.Electronics);
        var kettle = await TestData.ProductAsync(Db, 349m, stock: 5, category: ProductCategory.Homeware);

        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(earbuds.ProductId, 1));
        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(kettle.ProductId, 1));

        return (member, account, earbuds, kettle);
    }

    [Fact]
    public async Task Checkout_TakesStock_DebitsAccount_EarnsPoints_ClosesCart()
    {
        var (member, account, earbuds, _) = await CartWithR948Async();

        var result = await Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address()));

        result.Order.Total.ShouldBe(948m);
        result.Order.DeliveryFee.ShouldBe(0m);
        result.Order.Status.ShouldBe("Paid");
        result.Order.OrderNumber.ShouldStartWith("ORD-");
        result.Order.Items.Count.ShouldBe(2);
        result.PointsEarned.ShouldBe(100);
        result.AccountBalanceAfter.ShouldBe(9052m);

        using var db = Fresh();
        (await db.Products.SingleAsync(p => p.ProductId == earbuds.ProductId)).StockQuantity.ShouldBe(4);
        (await db.Carts.SingleAsync()).Status.ShouldBe(CartStatus.CheckedOut);
        (await db.Purchases.SingleAsync()).StoreId.ShouldBe(4);   // "SpendWell Online"
    }

    [Fact]
    public async Task Checkout_UnderR500_AddsDeliveryToTheDebit()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db, balance: 1000m);
        var product = await TestData.ProductAsync(Db, 200m);
        await Services.Carts.AddItemAsync(member.MemberId, new AddCartItemDto(product.ProductId, 1));

        var result = await Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address()));

        result.Order.Total.ShouldBe(260m);
        result.AccountBalanceAfter.ShouldBe(740m);

        using var db = Fresh();
        var purchase = await db.Purchases.Include(p => p.Items).SingleAsync();
        purchase.Amount.ShouldBe(260m);
        purchase.Items.ShouldContain(i => i.Category == ProductCategory.Delivery && i.LineTotal == 60m);
    }

    [Fact]
    public async Task Checkout_AfterwardsTheMemberGetsANewEmptyCart()
    {
        var (member, account, _, _) = await CartWithR948Async();
        var before = await Services.Carts.GetCartAsync(member.MemberId);

        await Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address()));
        var after = await Services.Carts.GetCartAsync(member.MemberId);

        after.CartId.ShouldNotBe(before.CartId);
        after.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Checkout_EmptyCart_Throws()
    {
        var (member, account) = await TestData.MemberWithAccountAsync(Db);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address())));
    }

    [Fact]
    public async Task Checkout_InsufficientFunds_Throws_StockAndCartUnchanged()
    {
        var (member, account, earbuds, _) = await CartWithR948Async(balance: 500m);

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address())));

        using var db = Fresh();
        (await db.Products.SingleAsync(p => p.ProductId == earbuds.ProductId)).StockQuantity.ShouldBe(5);
        (await db.Carts.SingleAsync()).Status.ShouldBe(CartStatus.Active);
        (await db.Orders.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Checkout_StockRanOutSinceAdding_Throws()
    {
        var (member, account, earbuds, _) = await CartWithR948Async();
        earbuds.StockQuantity = 0;
        await Db.SaveChangesAsync();

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address())));
    }

    [Fact]
    public async Task Cancel_BeforeShipping_RestocksRefundsAndClawsBackPoints()
    {
        var (member, account, earbuds, _) = await CartWithR948Async();
        var checkout = await Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address()));

        var result = await Services.Orders.CancelAsync(checkout.Order.Id, new CancelOrderDto("Changed my mind"));

        result.Order.Status.ShouldBe("Cancelled");
        result.AmountRefunded.ShouldBe(948m);
        result.AccountBalanceAfter.ShouldBe(10_000m);
        result.PointsReversed.ShouldBe(100);

        using var db = Fresh();
        (await db.Products.SingleAsync(p => p.ProductId == earbuds.ProductId)).StockQuantity.ShouldBe(5);
    }

    [Fact]
    public async Task Status_MovesOneStepAtATime()
    {
        var (member, account, _, _) = await CartWithR948Async();
        var checkout = await Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address()));
        int orderId = checkout.Order.Id;

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Orders.UpdateStatusAsync(orderId, new UpdateOrderStatusDto(OrderStatus.Delivered)));

        (await Services.Orders.UpdateStatusAsync(orderId, new UpdateOrderStatusDto(OrderStatus.Packed))).Status.ShouldBe("Packed");
        (await Services.Orders.UpdateStatusAsync(orderId, new UpdateOrderStatusDto(OrderStatus.Shipped))).Status.ShouldBe("Shipped");
        (await Services.Orders.UpdateStatusAsync(orderId, new UpdateOrderStatusDto(OrderStatus.Delivered))).Status.ShouldBe("Delivered");
    }

    [Fact]
    public async Task Cancel_AfterShipping_Throws()
    {
        var (member, account, _, _) = await CartWithR948Async();
        var checkout = await Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address()));
        await Services.Orders.UpdateStatusAsync(checkout.Order.Id, new UpdateOrderStatusDto(OrderStatus.Packed));
        await Services.Orders.UpdateStatusAsync(checkout.Order.Id, new UpdateOrderStatusDto(OrderStatus.Shipped));

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Orders.CancelAsync(checkout.Order.Id, new CancelOrderDto("Too late")));
    }

    [Fact]
    public async Task UpdateStatus_ToCancelled_IsRefused_UseCancelInstead()
    {
        var (member, account, _, _) = await CartWithR948Async();
        var checkout = await Services.Orders.CheckoutAsync(member.MemberId, new CheckoutDto(account.AccountNumber, TestData.Address()));

        await Should.ThrowAsync<BusinessRuleException>(() =>
            Services.Orders.UpdateStatusAsync(checkout.Order.Id, new UpdateOrderStatusDto(OrderStatus.Cancelled)));
    }
}
