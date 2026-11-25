using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface ICartService
{
    Task<CartDto> GetCartAsync(int memberId);
    Task<CartDto> AddItemAsync(int memberId, AddCartItemDto dto);
    Task<CartDto> UpdateItemAsync(int memberId, int productId, UpdateCartItemDto dto);
    Task<CartDto> RemoveItemAsync(int memberId, int productId);
    Task<CartDto> ClearAsync(int memberId);
}

/// <summary>
/// The cart reserves nothing: stock is only taken at checkout. The cart just shows
/// live prices and warns about anything that would block checkout.
/// </summary>
public class CartService : ICartService
{
    private readonly SpendWellDbContext _db;

    public CartService(SpendWellDbContext db)
    {
        _db = db;
    }

    public async Task<CartDto> GetCartAsync(int memberId)
    {
        var cart = await GetOrCreateActiveCartAsync(memberId);
        return BuildDto(cart);
    }

    /// <summary>Adds a product, or increases the quantity if it's already in the cart.</summary>
    public async Task<CartDto> AddItemAsync(int memberId, AddCartItemDto dto)
    {
        var cart = await GetOrCreateActiveCartAsync(memberId);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == dto.ProductId)
            ?? throw NotFoundException.For("Product", dto.ProductId);

        if (!product.IsActive)
        {
            throw new BusinessRuleException(string.Format("{0} is no longer sold.", product.Name));
        }

        var line = cart.Items.FirstOrDefault(i => i.ProductId == product.ProductId);
        int newQuantity = (line != null ? line.Quantity : 0) + dto.Quantity;

        EnsureQuantityAllowed(product, newQuantity);

        if (line == null)
        {
            line = new CartItem
            {
                Cart = cart,
                Product = product,
                Quantity = newQuantity,
                AddedAt = DateTime.UtcNow
            };
            cart.Items.Add(line);
        }
        else
        {
            line.Quantity = newQuantity;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return BuildDto(cart);
    }

    /// <summary>Sets the quantity of a line (not adds to it).</summary>
    public async Task<CartDto> UpdateItemAsync(int memberId, int productId, UpdateCartItemDto dto)
    {
        var cart = await GetOrCreateActiveCartAsync(memberId);

        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw NotFoundException.For("Cart item for product", productId);

        EnsureQuantityAllowed(line.Product, dto.Quantity);

        line.Quantity = dto.Quantity;
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return BuildDto(cart);
    }

    public async Task<CartDto> RemoveItemAsync(int memberId, int productId)
    {
        var cart = await GetOrCreateActiveCartAsync(memberId);

        var line = cart.Items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw NotFoundException.For("Cart item for product", productId);

        cart.Items.Remove(line);
        _db.CartItems.Remove(line);
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return BuildDto(cart);
    }

    public async Task<CartDto> ClearAsync(int memberId)
    {
        var cart = await GetOrCreateActiveCartAsync(memberId);

        _db.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return BuildDto(cart);
    }

    private static void EnsureQuantityAllowed(Product product, int quantity)
    {
        if (quantity > ShoppingRules.MaxQuantityPerLine)
        {
            throw new BusinessRuleException(string.Format(
                "You can buy at most {0} of one product per order.", ShoppingRules.MaxQuantityPerLine));
        }

        if (quantity > product.StockQuantity)
        {
            throw new BusinessRuleException(string.Format(
                "Only {0} of {1} in stock.", product.StockQuantity, product.Name));
        }
    }

    /// <summary>
    /// Loads the member's active cart with its products, creating one if needed.
    /// The filtered unique index means two requests can't both create a cart; the loser re-reads.
    /// </summary>
    private async Task<Cart> GetOrCreateActiveCartAsync(int memberId)
    {
        bool memberExists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!memberExists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        var cart = await LoadActiveCartAsync(memberId);
        if (cart != null)
        {
            return cart;
        }

        var now = DateTime.UtcNow;
        cart = new Cart
        {
            MemberId = memberId,
            Status = CartStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Carts.Add(cart);

        try
        {
            await _db.SaveChangesAsync();
            return cart;
        }
        catch (DbUpdateException)
        {
            // Another request created the cart first
            _db.Entry(cart).State = EntityState.Detached;
            return await LoadActiveCartAsync(memberId)
                ?? throw new ConflictException("Could not open a cart. Please try again.");
        }
    }

    private async Task<Cart?> LoadActiveCartAsync(int memberId)
    {
        return await _db.Carts
            .Include(c => c.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.MemberId == memberId && c.Status == CartStatus.Active);
    }

    private static CartDto BuildDto(Cart cart)
    {
        var items = new List<CartItemDto>();
        var warnings = new List<string>();

        foreach (var line in cart.Items.OrderBy(i => i.AddedAt))
        {
            var product = line.Product;
            bool available = product.IsActive && product.StockQuantity >= line.Quantity;

            if (!product.IsActive)
            {
                warnings.Add(string.Format("{0} is no longer sold. Remove it to check out.", product.Name));
            }
            else if (product.StockQuantity < line.Quantity)
            {
                warnings.Add(string.Format("Only {0} of {1} left in stock. Lower the quantity to check out.",
                    product.StockQuantity, product.Name));
            }

            items.Add(new CartItemDto(
                product.ProductId,
                product.Sku,
                product.Name,
                product.Category.ToString(),
                product.Price,
                line.Quantity,
                Math.Round(product.Price * line.Quantity, 2),
                available,
                product.StockQuantity));
        }

        decimal subtotal = items.Sum(i => i.LineTotal);
        decimal deliveryFee = ShoppingRules.DeliveryFeeFor(subtotal);

        return new CartDto(
            cart.CartId,
            cart.MemberId,
            cart.Status.ToString(),
            items,
            items.Sum(i => i.Quantity),
            subtotal,
            deliveryFee,
            subtotal + deliveryFee,
            ShoppingRules.AmountToFreeDelivery(subtotal),
            items.Count > 0 && warnings.Count == 0,
            warnings);
    }
}
