using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders)
    {
        _orders = orders;
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Get(int id)
    {
        return Ok(await _orders.GetAsync(id));
    }

    [HttpGet("~/api/members/{memberId:int}/orders")]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetForMember(
        int memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _orders.GetForMemberAsync(memberId, page, pageSize));
    }

    /// <summary>Warehouse/courier updates: Paid → Packed → Shipped → Delivered.</summary>
    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> UpdateStatus(int id, UpdateOrderStatusDto dto)
    {
        return Ok(await _orders.UpdateStatusAsync(id, dto));
    }

    /// <summary>Cancel before it ships: full refund, restock, and points clawed back if needed.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(OrderCancelResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderCancelResultDto>> Cancel(int id, CancelOrderDto dto)
    {
        return Ok(await _orders.CancelAsync(id, dto));
    }
}
