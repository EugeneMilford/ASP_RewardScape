using Microsoft.AspNetCore.Mvc;
using SpendWell.Api.Common;
using SpendWell.Api.DTOs;
using SpendWell.Api.Services;

namespace SpendWell.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Produces("application/json")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accounts;

    public AccountsController(IAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>Open another account for an existing member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AccountDto>> Open(OpenAccountDto dto)
    {
        var account = await _accounts.OpenAccountAsync(dto);
        return CreatedAtAction(nameof(Get), new { accountNumber = account.AccountNumber }, account);
    }

    [HttpGet("{accountNumber}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> Get(string accountNumber)
    {
        return Ok(await _accounts.GetByNumberAsync(accountNumber));
    }

    /// <summary>Put money into the account.</summary>
    [HttpPost("{accountNumber}/deposits")]
    public async Task<ActionResult<AccountTransactionDto>> Deposit(string accountNumber, DepositDto dto)
    {
        return Ok(await _accounts.DepositAsync(accountNumber, dto));
    }

    /// <summary>The account statement: every rand in and out, newest first, with the balance after each line.</summary>
    [HttpGet("{accountNumber}/transactions")]
    public async Task<ActionResult<PagedResult<AccountTransactionDto>>> GetTransactions(
        string accountNumber,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await _accounts.GetTransactionsAsync(accountNumber, from, to, page, pageSize));
    }

    /// <summary>
    /// How much money went out per purchase, per category and per store, plus points earned.
    /// Defaults to the current month when no dates are given.
    /// </summary>
    [HttpGet("{accountNumber}/spending-summary")]
    public async Task<ActionResult<SpendingSummaryDto>> GetSpendingSummary(
        string accountNumber,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var now = DateTime.UtcNow;
        var start = from ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = to ?? start.AddMonths(1);

        if (end <= start)
        {
            throw new BusinessRuleException("'to' must be after 'from'.");
        }

        return Ok(await _accounts.GetSpendingSummaryAsync(accountNumber, start, end));
    }
}
