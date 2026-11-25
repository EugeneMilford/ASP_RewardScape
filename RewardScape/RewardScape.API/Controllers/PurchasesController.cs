using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

[ApiController]
[Route("api/purchases")]
[Produces("application/json")]
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseService _purchases;

    public PurchasesController(IPurchaseService purchases)
    {
        _purchases = purchases;
    }

    /// <summary>
    /// Posted by the till. Debits the account, updates the month's spend and awards
    /// any milestone points crossed. Posting the same slip twice returns 409.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseResultDto>> Create(CreatePurchaseDto dto)
    {
        var result = await _purchases.CreateAsync(dto);
        return CreatedAtAction(nameof(Get), new { id = result.Purchase.Id }, result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PurchaseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseDto>> Get(int id)
    {
        return Ok(await _purchases.GetAsync(id));
    }

    /// <summary>
    /// Full or partial refund. Credits the account and claws back points if the
    /// purchase month drops below a milestone.
    /// </summary>
    [HttpPost("{id:int}/refunds")]
    [ProducesResponseType(typeof(RefundResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RefundResultDto>> Refund(int id, CreateRefundDto dto)
    {
        return Ok(await _purchases.RefundAsync(id, dto));
    }
}
