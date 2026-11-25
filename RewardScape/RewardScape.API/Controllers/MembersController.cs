using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

[ApiController]
[Route("api/members")]
[Produces("application/json")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _members;
    private readonly IAccountService _accounts;
    private readonly IPurchaseService _purchases;
    private readonly IDealService _deals;
    private readonly IRedemptionService _redemptions;

    public MembersController(
        IMemberService members,
        IAccountService accounts,
        IPurchaseService purchases,
        IDealService deals,
        IRedemptionService redemptions)
    {
        _members = members;
        _accounts = accounts;
        _purchases = purchases;
        _deals = deals;
        _redemptions = redemptions;
    }

    /// <summary>Sign up a member. Starts on Blue and opens a cheque account.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MemberDto>> Create(CreateMemberDto dto)
    {
        var member = await _members.CreateAsync(dto);
        return CreatedAtAction(nameof(Get), new { id = member.Id }, member);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberDto>> Get(int id)
    {
        return Ok(await _members.GetAsync(id));
    }

    /// <summary>All accounts linked to the member.</summary>
    [HttpGet("{id:int}/accounts")]
    public async Task<ActionResult<IReadOnlyList<AccountSummaryDto>>> GetAccounts(int id)
    {
        return Ok(await _accounts.GetForMemberAsync(id));
    }

    /// <summary>
    /// Points earned from shopping, travel, driving and bonuses (this month and this year),
    /// spendable balance, and points still needed for the next tier.
    /// </summary>
    [HttpGet("{id:int}/rewards-summary")]
    public async Task<ActionResult<RewardsSummaryDto>> GetRewardsSummary(int id, [FromQuery] int? year)
    {
        return Ok(await _members.GetRewardsSummaryAsync(id, year));
    }

    /// <summary>Spendable points, points owed, and what expires in the next 30 days.</summary>
    [HttpGet("{id:int}/points")]
    public async Task<ActionResult<PointsBalanceDto>> GetPoints(int id)
    {
        return Ok(await _members.GetPointsBalanceAsync(id));
    }

    /// <summary>Every points movement: earn, redeem, reverse, expire, restore.</summary>
    [HttpGet("{id:int}/points/ledger")]
    public async Task<ActionResult<PagedResult<PointsTransactionDto>>> GetPointsLedger(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _members.GetPointsLedgerAsync(id, page, pageSize));
    }

    /// <summary>Points lots in expiry order (the order they'll be spent in).</summary>
    [HttpGet("{id:int}/points/lots")]
    public async Task<ActionResult<IReadOnlyList<PointsLotDto>>> GetPointsLots(int id, [FromQuery] bool includeEmpty = false)
    {
        return Ok(await _members.GetPointsLotsAsync(id, includeEmpty));
    }

    /// <summary>"R1 150 spent this month, R1 350 to the next milestone."</summary>
    [HttpGet("{id:int}/progress")]
    public async Task<ActionResult<MonthlyProgressDto>> GetProgress(int id, [FromQuery] int? year, [FromQuery] int? month)
    {
        return Ok(await _members.GetMonthlyProgressAsync(id, year, month));
    }

    [HttpGet("{id:int}/tier-history")]
    public async Task<ActionResult<IReadOnlyList<TierHistoryDto>>> GetTierHistory(int id)
    {
        return Ok(await _members.GetTierHistoryAsync(id));
    }

    [HttpGet("{id:int}/purchases")]
    public async Task<ActionResult<PagedResult<PurchaseDto>>> GetPurchases(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _purchases.GetForMemberAsync(id, page, pageSize));
    }

    /// <summary>
    /// Live deals priced for this member's tier, with a reason when one can't be redeemed.
    /// ?onlyRedeemable=true shows just the ones they can redeem now.
    /// </summary>
    [HttpGet("{id:int}/deals")]
    public async Task<ActionResult<IReadOnlyList<MemberDealDto>>> GetDeals(int id, [FromQuery] bool onlyRedeemable = false)
    {
        return Ok(await _deals.GetDealsForMemberAsync(id, onlyRedeemable));
    }

    [HttpGet("{id:int}/redemptions")]
    public async Task<ActionResult<IReadOnlyList<RedemptionDto>>> GetRedemptions(int id, [FromQuery] RedemptionStatus? status)
    {
        return Ok(await _redemptions.GetForMemberAsync(id, status));
    }
}
