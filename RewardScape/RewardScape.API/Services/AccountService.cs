using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IAccountService
{
    Task<AccountDto> OpenAccountAsync(OpenAccountDto dto);
    Task<AccountDto> GetByNumberAsync(string accountNumber);
    Task<IReadOnlyList<AccountSummaryDto>> GetForMemberAsync(int memberId);
    Task<AccountTransactionDto> DepositAsync(string accountNumber, DepositDto dto);
    Task<PagedResult<AccountTransactionDto>> GetTransactionsAsync(string accountNumber, DateTime? from, DateTime? to, int page, int pageSize);
    Task<SpendingSummaryDto> GetSpendingSummaryAsync(string accountNumber, DateTime from, DateTime to);
    Task<Account> CreateAccountEntityAsync(Member member, AccountType type, decimal openingDeposit);
}

public class AccountService : IAccountService
{
    private readonly SpendWellDbContext _db;

    public AccountService(SpendWellDbContext db)
    {
        _db = db;
    }

    public async Task<AccountDto> OpenAccountAsync(OpenAccountDto dto)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.MemberId == dto.MemberId)
            ?? throw NotFoundException.For("Member", dto.MemberId);

        var account = await CreateAccountEntityAsync(member, dto.AccountType, dto.OpeningDeposit);
        await _db.SaveChangesAsync();

        account.Member = member;
        return account.ToDto();
    }

    /// <summary>
    /// Builds a new account with a unique number and an optional opening deposit.
    /// Shared with MemberService so sign-up and "open another account" use the same rules.
    /// Does not call SaveChanges.
    /// </summary>
    public async Task<Account> CreateAccountEntityAsync(Member member, AccountType type, decimal openingDeposit)
    {
        var account = new Account
        {
            AccountNumber = await GenerateUniqueAccountNumberAsync(),
            AccountType = type,
            Status = AccountStatus.Active,
            Balance = 0m,
            OpenedAt = DateTime.UtcNow,
            Member = member
        };

        _db.Accounts.Add(account);

        if (openingDeposit > 0)
        {
            AccountPosting.Credit(account, openingDeposit, AccountTransactionType.Deposit,
                "OPENING", "Opening deposit");
        }

        return account;
    }

    public async Task<AccountDto> GetByNumberAsync(string accountNumber)
    {
        var account = await _db.Accounts
            .Include(a => a.Member)
            .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber)
            ?? throw NotFoundException.For("Account", accountNumber);

        return account.ToDto();
    }

    public async Task<IReadOnlyList<AccountSummaryDto>> GetForMemberAsync(int memberId)
    {
        bool exists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!exists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        return await _db.Accounts
            .Where(a => a.MemberId == memberId)
            .OrderBy(a => a.OpenedAt)
            .Select(a => a.ToSummaryDto())
            .ToListAsync();
    }

    public async Task<AccountTransactionDto> DepositAsync(string accountNumber, DepositDto dto)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber)
            ?? throw NotFoundException.For("Account", accountNumber);

        AccountPosting.EnsureUsable(account);

        string description = string.IsNullOrWhiteSpace(dto.Description) ? "Deposit" : dto.Description;
        string reference = string.Format("DEP-{0:yyyyMMddHHmmss}", DateTime.UtcNow);

        var transaction = AccountPosting.Credit(account, dto.Amount, AccountTransactionType.Deposit, reference, description);
        await _db.SaveChangesAsync();

        return transaction.ToDto();
    }

    public async Task<PagedResult<AccountTransactionDto>> GetTransactionsAsync(
        string accountNumber, DateTime? from, DateTime? to, int page, int pageSize)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber)
            ?? throw NotFoundException.For("Account", accountNumber);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AccountTransactions.Where(t => t.AccountId == account.AccountId);

        if (from.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(t => t.CreatedAt < to.Value);
        }

        int total = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.AccountTransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AccountTransactionDto>(
            items.Select(t => t.ToDto()).ToList(), page, pageSize, total);
    }

    /// <summary>
    /// How much money left the account between two dates, broken down per purchase,
    /// per category and per store, with the points each purchase earned.
    /// </summary>
    public async Task<SpendingSummaryDto> GetSpendingSummaryAsync(string accountNumber, DateTime from, DateTime to)
    {
        if (to <= from)
        {
            throw new BusinessRuleException("'to' must be after 'from'.");
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber)
            ?? throw NotFoundException.For("Account", accountNumber);

        var transactions = await _db.AccountTransactions
            .Where(t => t.AccountId == account.AccountId && t.CreatedAt >= from && t.CreatedAt < to)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.AccountTransactionId)
            .ToListAsync();

        // Opening balance = balance after the last transaction before the period
        var lastBefore = await _db.AccountTransactions
            .Where(t => t.AccountId == account.AccountId && t.CreatedAt < from)
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.AccountTransactionId)
            .FirstOrDefaultAsync();

        decimal openingBalance = lastBefore != null ? lastBefore.BalanceAfter : 0m;
        decimal closingBalance = transactions.Count > 0 ? transactions[transactions.Count - 1].BalanceAfter : openingBalance;

        decimal moneyIn = 0m;
        decimal moneyOut = 0m;
        decimal spentOnPurchases = 0m;
        decimal refunded = 0m;
        decimal spentOnTravel = 0m;
        decimal travelRefunded = 0m;

        foreach (var t in transactions)
        {
            switch (t.Direction)
            {
                case TransactionDirection.Credit:
                    moneyIn += t.Amount;
                    break;
                case TransactionDirection.Debit:
                    moneyOut += t.Amount;
                    break;
            }

            switch (t.Type)
            {
                case AccountTransactionType.PurchaseDebit:
                    spentOnPurchases += t.Amount;
                    break;
                case AccountTransactionType.RefundCredit:
                    refunded += t.Amount;
                    break;
                case AccountTransactionType.TravelPayment:
                    spentOnTravel += t.Amount;
                    break;
                case AccountTransactionType.TravelRefund:
                    travelRefunded += t.Amount;
                    break;
            }
        }

        var purchases = await _db.Purchases
            .Include(p => p.Store)
            .Include(p => p.Items)
            .Include(p => p.Refunds)
            .Where(p => p.AccountId == account.AccountId && p.PurchasedAt >= from && p.PurchasedAt < to)
            .OrderBy(p => p.PurchasedAt)
            .ToListAsync();

        var debitByPurchase = transactions
            .Where(t => t.Type == AccountTransactionType.PurchaseDebit && t.PurchaseId.HasValue)
            .ToDictionary(t => t.PurchaseId!.Value, t => t);

        var lines = new List<SpendLineDto>();
        foreach (var p in purchases)
        {
            decimal balanceAfter = debitByPurchase.TryGetValue(p.PurchaseId, out var debit) ? debit.BalanceAfter : 0m;

            lines.Add(new SpendLineDto(
                p.PurchaseId,
                p.PurchasedAt,
                p.Store.Name,
                p.ExternalReference,
                p.Amount,
                p.RefundedAmount,
                p.Amount - p.RefundedAmount,
                p.PointsEarned,
                balanceAfter));
        }

        // Category split uses item totals. A partial refund isn't tied to an item, so it is
        // spread across the purchase's categories in proportion to their value.
        var categoryTotals = new Dictionary<ProductCategory, decimal>();
        foreach (var p in purchases)
        {
            decimal keepRatio = p.Amount == 0 ? 0 : (p.Amount - p.RefundedAmount) / p.Amount;
            foreach (var item in p.Items)
            {
                decimal value = item.LineTotal * keepRatio;
                if (categoryTotals.ContainsKey(item.Category))
                {
                    categoryTotals[item.Category] += value;
                }
                else
                {
                    categoryTotals[item.Category] = value;
                }
            }
        }

        decimal netSpend = spentOnPurchases - refunded;

        var byCategory = categoryTotals
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new CategorySpendDto(
                kv.Key.ToString(),
                Math.Round(kv.Value, 2),
                netSpend == 0 ? 0 : Math.Round(kv.Value / netSpend * 100, 1)))
            .ToList();

        var byStore = purchases
            .GroupBy(p => p.Store.Name)
            .Select(g => new StoreSpendDto(g.Key, g.Count(), g.Sum(p => p.Amount - p.RefundedAmount)))
            .OrderByDescending(s => s.Amount)
            .ToList();

        // Travel booked from this account in the period
        var travelBookings = await _db.TravelBookings
            .Include(b => b.Listing)
            .Where(b => b.AccountId == account.AccountId && b.BookedAt >= from && b.BookedAt < to)
            .OrderBy(b => b.BookedAt)
            .ToListAsync();

        var travelLines = travelBookings
            .Select(b => new TravelSpendLineDto(
                b.BookingReference,
                b.Type.ToString(),
                b.Listing.Title,
                b.BookedAt,
                b.TotalPaid,
                b.RefundedAmount,
                b.TotalPaid - b.RefundedAmount,
                b.Status.ToString(),
                b.PointsAwarded))
            .ToList();

        int pointsEarned = purchases.Sum(p => p.PointsEarned) + travelBookings.Sum(b => b.PointsAwarded);

        return new SpendingSummaryDto(
            account.AccountNumber,
            from,
            to,
            openingBalance,
            closingBalance,
            moneyIn,
            moneyOut,
            spentOnPurchases,
            refunded,
            netSpend,
            spentOnTravel,
            travelRefunded,
            spentOnTravel - travelRefunded,
            pointsEarned,
            byCategory,
            byStore,
            lines,
            travelLines);
    }

    /// <summary>10 digits starting with 62, checked against the database for clashes.</summary>
    private async Task<string> GenerateUniqueAccountNumberAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            int body = RandomNumberGenerator.GetInt32(0, 100_000_000);
            string candidate = string.Format("62{0:D8}", body);

            bool taken = await _db.Accounts.AnyAsync(a => a.AccountNumber == candidate)
                || _db.Accounts.Local.Any(a => a.AccountNumber == candidate);

            if (!taken)
            {
                return candidate;
            }
        }

        throw new ConflictException("Could not generate a unique account number. Please try again.");
    }
}
