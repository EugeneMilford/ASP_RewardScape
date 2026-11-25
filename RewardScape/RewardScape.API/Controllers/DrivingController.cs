using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

/// <summary>
/// Driving rewards: register a car, the tracker uploads trips, each trip is scored,
/// and the month's average score pays Driving points when the month closes.
/// </summary>
[ApiController]
[Route("api/driving")]
[Produces("application/json")]
public class DrivingController : ControllerBase
{
    private readonly IDrivingService _driving;

    public DrivingController(IDrivingService driving)
    {
        _driving = driving;
    }

    [HttpPost("vehicles")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VehicleDto>> RegisterVehicle(RegisterVehicleDto dto)
    {
        var vehicle = await _driving.RegisterVehicleAsync(dto);
        return CreatedAtAction(nameof(GetVehicles), new { memberId = vehicle.MemberId }, vehicle);
    }

    [HttpGet("~/api/members/{memberId:int}/vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetVehicles(int memberId)
    {
        return Ok(await _driving.GetVehiclesAsync(memberId));
    }

    [HttpPost("vehicles/{id:int}/deactivate")]
    public async Task<ActionResult<VehicleDto>> DeactivateVehicle(int id)
    {
        return Ok(await _driving.DeactivateVehicleAsync(id));
    }

    /// <summary>
    /// Called by the telematics device or phone app after each trip.
    /// Re-sending the same ExternalTripId returns 409, not a second trip.
    /// </summary>
    [HttpPost("trips")]
    [ProducesResponseType(typeof(DrivingTripDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DrivingTripDto>> UploadTrip(UploadTripDto dto)
    {
        return Ok(await _driving.UploadTripAsync(dto));
    }

    [HttpGet("~/api/members/{memberId:int}/driving/trips")]
    public async Task<ActionResult<PagedResult<DrivingTripDto>>> GetTrips(
        int memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _driving.GetTripsAsync(memberId, page, pageSize));
    }

    /// <summary>
    /// This month so far (or ?year=&amp;month= for a past month): km driven, average score,
    /// band reached, points it will pay, and how far to the next band.
    /// </summary>
    [HttpGet("~/api/members/{memberId:int}/driving/summary")]
    public async Task<ActionResult<DrivingMonthSummaryDto>> GetSummary(int memberId, [FromQuery] int? year, [FromQuery] int? month)
    {
        return Ok(await _driving.GetMonthSummaryAsync(memberId, year, month));
    }

    [HttpGet("bands")]
    public async Task<ActionResult<IReadOnlyList<DrivingRewardBandDto>>> GetBands()
    {
        return Ok(await _driving.GetBandsAsync());
    }
}
