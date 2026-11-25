using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

[ApiController]
[Route("api/redemptions")]
[Produces("application/json")]
public class RedemptionsController : ControllerBase
{
    private readonly IRedemptionService _redemptions;

    public RedemptionsController(IRedemptionService redemptions)
    {
        _redemptions = redemptions;
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RedemptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RedemptionDto>> Get(int id)
    {
        return Ok(await _redemptions.GetAsync(id));
    }

    /// <summary>Cancel an unused voucher. Points and any cash go back.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(typeof(RedemptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RedemptionDto>> Cancel(int id)
    {
        return Ok(await _redemptions.CancelAsync(id));
    }

    /// <summary>Scan a voucher at the partner (or at a SpendWell till for in-store deals).</summary>
    [HttpPost("vouchers/{code}/use")]
    [ProducesResponseType(typeof(RedemptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RedemptionDto>> UseVoucher(string code, UseVoucherDto dto)
    {
        return Ok(await _redemptions.UseVoucherAsync(code, dto));
    }
}
