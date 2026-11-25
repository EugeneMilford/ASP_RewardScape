using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

/// <summary>
/// Back-office endpoints: milestones, tiers, stores, deals, products, travel and jobs.
/// Lock this down with an Admin role once auth is added.
/// </summary>
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly SpendWellDbContext _db;
    private readonly ITierService _tiers;
    private readonly IDealService _deals;
    private readonly IExpiryService _expiry;
    private readonly IProductService _products;
    private readonly ITravelService _travel;
    private readonly IDrivingService _driving;

    public AdminController(
        SpendWellDbContext db,
        ITierService tiers,
        IDealService deals,
        IExpiryService expiry,
        IProductService products,
        ITravelService travel,
        IDrivingService driving)
    {
        _db = db;
        _tiers = tiers;
        _deals = deals;
        _expiry = expiry;
        _products = products;
        _travel = travel;
        _driving = driving;
    }

    // ---------- Milestones ----------

    [HttpGet("milestones")]
    public async Task<ActionResult<IReadOnlyList<MilestoneDto>>> GetMilestones()
    {
        var milestones = await _db.SpendMilestones.ToListAsync();
        return Ok(milestones.OrderBy(m => m.ThresholdAmount).Select(m => m.ToDto()).ToList());
    }

    /// <summary>
    /// Add a milestone. Points must keep rising with the threshold, otherwise
    /// spending more could earn fewer points.
    /// </summary>
    [HttpPost("milestones")]
    public async Task<ActionResult<MilestoneDto>> CreateMilestone(CreateMilestoneDto dto)
    {
        var active = await _db.SpendMilestones.Where(m => m.IsActive).ToListAsync();

        if (active.Any(m => m.ThresholdAmount == dto.ThresholdAmount))
        {
            throw new ConflictException(string.Format("A milestone at R{0:N2} already exists.", dto.ThresholdAmount));
        }

        var below = active.Where(m => m.ThresholdAmount < dto.ThresholdAmount).OrderByDescending(m => m.ThresholdAmount).FirstOrDefault();
        var above = active.Where(m => m.ThresholdAmount > dto.ThresholdAmount).OrderBy(m => m.ThresholdAmount).FirstOrDefault();

        if ((below != null && dto.CumulativePoints <= below.CumulativePoints)
            || (above != null && dto.CumulativePoints >= above.CumulativePoints))
        {
            throw new BusinessRuleException("Milestone points must increase as the spend threshold increases.");
        }

        var milestone = new SpendMilestone
        {
            ThresholdAmount = dto.ThresholdAmount,
            CumulativePoints = dto.CumulativePoints,
            IsActive = true
        };

        _db.SpendMilestones.Add(milestone);
        await _db.SaveChangesAsync();

        return Ok(milestone.ToDto());
    }

    [HttpPost("milestones/{id:int}/deactivate")]
    public async Task<ActionResult<MilestoneDto>> DeactivateMilestone(int id)
    {
        var milestone = await _db.SpendMilestones.FirstOrDefaultAsync(m => m.SpendMilestoneId == id)
            ?? throw NotFoundException.For("Milestone", id);

        milestone.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(milestone.ToDto());
    }

    // ---------- Tiers ----------

    [HttpGet("tiers")]
    public async Task<ActionResult<IReadOnlyList<TierDto>>> GetTiers()
    {
        return Ok(await _tiers.GetTiersAsync());
    }

    /// <summary>Annual review: moves every member up or down to the tier their last 12 months earned.</summary>
    [HttpPost("tiers/review")]
    public async Task<ActionResult<TierReviewResultDto>> RunTierReview()
    {
        return Ok(await _tiers.RunAnnualReviewAsync());
    }

    // ---------- Stores ----------

    [HttpGet("stores")]
    public async Task<ActionResult<IReadOnlyList<StoreDto>>> GetStores()
    {
        var stores = await _db.Stores.OrderBy(s => s.Name).ToListAsync();
        return Ok(stores.Select(s => s.ToDto()).ToList());
    }

    [HttpPost("stores")]
    public async Task<ActionResult<StoreDto>> CreateStore(CreateStoreDto dto)
    {
        var store = new Store { Name = dto.Name, Region = dto.Region, IsActive = true };
        _db.Stores.Add(store);
        await _db.SaveChangesAsync();
        return Ok(store.ToDto());
    }

    // ---------- Deals ----------

    [HttpPost("deals")]
    public async Task<ActionResult<DealDto>> CreateDeal(CreateDealDto dto)
    {
        return Ok(await _deals.CreateAsync(dto));
    }

    [HttpPut("deals/{id:int}/stock")]
    public async Task<ActionResult<DealDto>> UpdateDealStock(int id, UpdateDealStockDto dto)
    {
        return Ok(await _deals.UpdateStockAsync(id, dto));
    }

    // ---------- Shop products ----------

    [HttpPost("products")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> CreateProduct(CreateProductDto dto)
    {
        return Ok(await _products.CreateAsync(dto));
    }

    [HttpPut("products/{id:int}")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(int id, UpdateProductDto dto)
    {
        return Ok(await _products.UpdateAsync(id, dto));
    }

    [HttpPost("products/{id:int}/restock")]
    public async Task<ActionResult<ProductDto>> RestockProduct(int id, RestockDto dto)
    {
        return Ok(await _products.RestockAsync(id, dto));
    }

    // ---------- Travel ----------

    [HttpGet("travel/providers")]
    public async Task<ActionResult<IReadOnlyList<TravelProviderDto>>> GetTravelProviders()
    {
        return Ok(await _travel.GetProvidersAsync());
    }

    [HttpPost("travel/providers")]
    public async Task<ActionResult<TravelProviderDto>> CreateTravelProvider(CreateTravelProviderDto dto)
    {
        return Ok(await _travel.CreateProviderAsync(dto));
    }

    [HttpPost("travel/listings")]
    public async Task<ActionResult<TravelListingDto>> CreateTravelListing(CreateTravelListingDto dto)
    {
        return Ok(await _travel.CreateListingAsync(dto));
    }

    /// <summary>Points per R10 for each travel type (before tier multiplier).</summary>
    [HttpGet("travel/earn-rules")]
    public async Task<ActionResult<IReadOnlyList<TravelEarnRuleDto>>> GetTravelEarnRules()
    {
        return Ok(await _travel.GetEarnRulesAsync());
    }

    [HttpPut("travel/earn-rules/{type}")]
    public async Task<ActionResult<TravelEarnRuleDto>> UpdateTravelEarnRule(TravelType type, UpdateTravelEarnRuleDto dto)
    {
        return Ok(await _travel.UpdateEarnRuleAsync(type, dto));
    }

    // ---------- Jobs ----------

    /// <summary>Run the expiry job now instead of waiting for the 6-hourly background run.</summary>
    [HttpPost("jobs/expire")]
    public async Task<ActionResult<ExpiryRunResultDto>> RunExpiry()
    {
        return Ok(await _expiry.RunAsync(HttpContext.RequestAborted));
    }

    /// <summary>Complete every finished trip and pay its Travel points now.</summary>
    [HttpPost("jobs/complete-travel")]
    public async Task<ActionResult<TravelCompletionRunDto>> RunTravelCompletion()
    {
        return Ok(await _travel.CompleteFinishedTripsAsync(HttpContext.RequestAborted));
    }

    /// <summary>Close a driving month and pay Driving points. Only allowed 2 days after the month ends.</summary>
    [HttpPost("jobs/close-driving-month")]
    public async Task<ActionResult<DrivingMonthRunResultDto>> CloseDrivingMonth([FromQuery] int year, [FromQuery] int month)
    {
        return Ok(await _driving.FinaliseMonthAsync(year, month));
    }
}
