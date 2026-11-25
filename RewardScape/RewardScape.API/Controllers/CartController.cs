using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

/// <summary>
/// A member's shopping cart. Every call returns the whole cart with live prices,
/// delivery fee and any warnings, so the app can just re-render it.
/// </summary>
[ApiController]
[Route("api/members/{memberId:int}/cart")]
[Produces("application/json")]
public class CartController : ControllerBase
{
    private readonly ICartService _carts;
    private readonly IOrderService _orders;

    public CartController(ICartService carts, IOrderService orders)
    {
        _carts = carts;
        _orders = orders;
    }

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get(int memberId)
    {
        return Ok(await _carts.GetCartAsync(memberId));
    }

    /// <summary>Add a product. If it's already in the cart, the quantity goes up.</summary>
    [HttpPost("items")]
    [ProducesResponseType(typeof(CartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CartDto>> AddItem(int memberId, AddCartItemDto dto)
    {
        return Ok(await _carts.AddItemAsync(memberId, dto));
    }

    /// <summary>Set a line's quantity.</summary>
    [HttpPut("items/{productId:int}")]
    public async Task<ActionResult<CartDto>> UpdateItem(int memberId, int productId, UpdateCartItemDto dto)
    {
        return Ok(await _carts.UpdateItemAsync(memberId, productId, dto));
    }

    [HttpDelete("items/{productId:int}")]
    public async Task<ActionResult<CartDto>> RemoveItem(int memberId, int productId)
    {
        return Ok(await _carts.RemoveItemAsync(memberId, productId));
    }

    [HttpDelete]
    public async Task<ActionResult<CartDto>> Clear(int memberId)
    {
        return Ok(await _carts.ClearAsync(memberId));
    }

    /// <summary>
    /// Pay for the cart from the member's account. Takes stock, creates the order,
    /// debits the account and awards shopping milestone points, all or nothing.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType(typeof(CheckoutResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CheckoutResultDto>> Checkout(int memberId, CheckoutDto dto)
    {
        var result = await _orders.CheckoutAsync(memberId, dto);
        return CreatedAtAction(nameof(OrdersController.Get), "Orders", new { id = result.Order.Id }, result);
    }
}
