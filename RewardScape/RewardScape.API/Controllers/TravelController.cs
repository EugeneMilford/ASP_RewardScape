using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

/// <summary>
/// Flights, hotels and car hire. Search → quote → book → (cancel) → complete.
/// Providers, listings and earn rules are managed under /api/admin/travel.
/// </summary>
[ApiController]
[Route("api/travel")]
[Produces("application/json")]
public class TravelController : ControllerBase
{
    private readonly ITravelService _travel;

    public TravelController(ITravelService travel)
    {
        _travel = travel;
    }

    /// <summary>Search: ?type=Hotel&amp;destination=Knysna&amp;from=2026-12-20&amp;to=2026-12-27</summary>
    [HttpGet("listings")]
    public async Task<ActionResult<IReadOnlyList<TravelListingDto>>> Search([FromQuery] TravelListingQueryDto query)
    {
        return Ok(await _travel.SearchAsync(query));
    }

    [HttpGet("listings/{id:int}")]
    [ProducesResponseType(typeof(TravelListingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TravelListingDto>> GetListing(int id)
    {
        return Ok(await _travel.GetListingAsync(id));
    }

    /// <summary>How many seats/rooms/cars are free. Hotels and car hire need from and to dates.</summary>
    [HttpGet("listings/{id:int}/availability")]
    public async Task<ActionResult<AvailabilityDto>> GetAvailability(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        return Ok(await _travel.GetAvailabilityAsync(id, from, to));
    }

    /// <summary>Price after tier discount and the points the trip will earn. Books nothing.</summary>
    [HttpPost("quote")]
    public async Task<ActionResult<TravelQuoteDto>> Quote(TravelQuoteRequestDto dto)
    {
        return Ok(await _travel.QuoteAsync(dto));
    }

    /// <summary>Book and pay from the member's account. Points are paid when the trip is completed.</summary>
    [HttpPost("bookings")]
    [ProducesResponseType(typeof(TravelBookingResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TravelBookingResultDto>> Book(CreateTravelBookingDto dto)
    {
        var result = await _travel.BookAsync(dto);
        return CreatedAtAction(nameof(GetBooking), new { id = result.Booking.Id }, result);
    }

    [HttpGet("bookings/{id:int}")]
    [ProducesResponseType(typeof(TravelBookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TravelBookingDto>> GetBooking(int id)
    {
        return Ok(await _travel.GetBookingAsync(id));
    }

    [HttpGet("~/api/members/{memberId:int}/travel-bookings")]
    public async Task<ActionResult<IReadOnlyList<TravelBookingDto>>> GetForMember(int memberId, [FromQuery] TravelBookingStatus? status)
    {
        return Ok(await _travel.GetForMemberAsync(memberId, status));
    }

    /// <summary>Free up to 48 hours before the start; 50% fee after that.</summary>
    [HttpPost("bookings/{id:int}/cancel")]
    [ProducesResponseType(typeof(CancelTravelBookingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CancelTravelBookingResultDto>> Cancel(int id)
    {
        return Ok(await _travel.CancelAsync(id));
    }

    /// <summary>
    /// Mark a finished trip complete and pay its Travel points.
    /// The background job does this automatically; this is for testing or manual fixes.
    /// </summary>
    [HttpPost("bookings/{id:int}/complete")]
    [ProducesResponseType(typeof(TravelBookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TravelBookingDto>> Complete(int id)
    {
        return Ok(await _travel.CompleteAsync(id));
    }
}
