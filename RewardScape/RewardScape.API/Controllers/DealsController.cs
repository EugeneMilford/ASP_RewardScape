using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

[ApiController]
[Route("api/deals")]
[Produces("application/json")]
public class DealsController : ControllerBase
{
    private readonly IDealService _deals;
    private readonly IRedemptionService _redemptions;

    public DealsController(IDealService deals, IRedemptionService redemptions)
    {
        _deals = deals;
        _redemptions = redemptions;
    }

    /// <summary>The deal catalogue. Filter by tier name (e.g. Gold) or partner category.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DealDto>>> GetAll([FromQuery] DealQueryDto query)
    {
        return Ok(await _deals.GetDealsAsync(query));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealDto>> Get(int id)
    {
        return Ok(await _deals.GetAsync(id));
    }

    /// <summary>
    /// Spend points (and any cash top-up) on a deal and receive a voucher code.
    /// </summary>
    [HttpPost("{id:int}/redeem")]
    [ProducesResponseType(typeof(RedemptionResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RedemptionResultDto>> Redeem(int id, RedeemDealDto dto)
    {
        var result = await _redemptions.RedeemAsync(id, dto);
        return CreatedAtAction(
            nameof(RedemptionsController.Get),
            "Redemptions",
            new { id = result.Redemption.Id },
            result);
    }
}
