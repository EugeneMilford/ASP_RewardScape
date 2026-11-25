using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

public interface IRedemptionService
{
    Task<RedemptionResultDto> RedeemAsync(int dealId, RedeemDealDto dto);
    Task<RedemptionDto> CancelAsync(int redemptionId);
    Task<RedemptionDto> UseVoucherAsync(string voucherCode, UseVoucherDto dto);
    Task<RedemptionDto> GetAsync(int redemptionId);
    Task<IReadOnlyList<RedemptionDto>> GetForMemberAsync(int memberId, RedemptionStatus? status);
}

public class RedemptionService : IRedemptionService
{
    private const string VoucherAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O or 1/I mix-ups

    private readonly SpendWellDbContext _db;
    private readonly IPointsService _points;
    private readonly IDealService _deals;

    public RedemptionService(SpendWellDbContext db, IPointsService points, IDealService deals)
    {
        _db = db;
        _points = points;
        _deals = deals;
    }

    /// <summary>
    /// Spends points (oldest-expiring first), takes any cash top-up from the account,
    /// decrements stock and issues a voucher, all in one transaction.
    /// </summary>
    public async Task<RedemptionResultDto> RedeemAsync(int dealId, RedeemDealDto dto)
    {
        var now = DateTime.UtcNow;

        var deal = await _db.Deals
            .Include(d => d.MinTier)
            .FirstOrDefaultAsync(d => d.DealId == dealId)
            ?? throw NotFoundException.For("Deal", dealId);

        var member = await _db.Members
            .Include(m => m.Tier)
            .FirstOrDefaultAsync(m => m.MemberId == dto.MemberId)
            ?? throw NotFoundException.For("Member", dto.MemberId);

        // 1. Deal rules
        if (!deal.IsActive || now < deal.StartsAt || now >= deal.EndsAt)
        {
            throw new BusinessRuleException("This deal is not currently running.");
        }

        if (deal.StockRemaining <= 0)
        {
            throw new BusinessRuleException("This deal is sold out.");
        }

        if (member.Tier.Rank < deal.MinTier.Rank)
        {
            throw new BusinessRuleException(string.Format(
                "This deal is for {0} members and above. You are {1}.", deal.MinTier.Name, member.Tier.Name));
        }

        int alreadyRedeemed = await _db.Redemptions.CountAsync(r =>
            r.MemberId == member.MemberId && r.DealId == deal.DealId && r.Status != RedemptionStatus.Cancelled);

        if (alreadyRedeemed >= deal.MaxPerMember)
        {
            throw new BusinessRuleException(string.Format(
                "You have reached the limit of {0} for this deal.", deal.MaxPerMember));
        }

        if (member.PointsOwed > 0)
        {
            throw new BusinessRuleException(string.Format(
                "You owe {0} points from a refund. New earnings will clear this before you can redeem.", member.PointsOwed));
        }

        // 2. Cash part, if any
        decimal cashToPay = _deals.CashPriceForTier(deal, member.Tier);
        Account? account = null;

        if (cashToPay > 0)
        {
            if (string.IsNullOrWhiteSpace(dto.AccountNumber))
            {
                throw new BusinessRuleException(string.Format(
                    "This deal has a R{0:N2} cash top-up. Please supply an account number.", cashToPay));
            }

            account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == dto.AccountNumber)
                ?? throw NotFoundException.For("Account", dto.AccountNumber);

            if (account.MemberId != member.MemberId)
            {
                throw new BusinessRuleException("This account does not belong to the member.");
            }
        }

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        // 3. Voucher + stock. Deal.RowVersion stops two people taking the last one.
        var redemption = new Redemption
        {
            Member = member,
            Deal = deal,
            Account = account,
            PointsSpent = deal.PointsCost,
            CashPaid = cashToPay,
            VoucherCode = await GenerateUniqueVoucherCodeAsync(),
            Status = RedemptionStatus.Issued,
            IssuedAt = now,
            ExpiresAt = now.AddDays(deal.VoucherValidDays)
        };

        _db.Redemptions.Add(redemption);
        deal.StockRemaining -= 1;

        await _db.SaveChangesAsync();

        // 4. Points out (FIFO) and cash out
        var lotsUsed = await _points.ConsumeAsync(member, deal.PointsCost, redemption);

        if (account != null)
        {
            var debit = AccountPosting.Debit(account, cashToPay, AccountTransactionType.DealCashPayment,
                redemption.VoucherCode, string.Format("Deal top-up: {0}", deal.Title));
            debit.Redemption = redemption;
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        int pointsAfter = await _points.GetAvailablePointsAsync(member.MemberId);

        return new RedemptionResultDto(
            redemption.ToDto(),
            pointsAfter,
            account != null ? account.Balance : null,
            lotsUsed);
    }

    /// <summary>
    /// Cancels an unused voucher: points go back to the lots that paid (traced through the
    /// per-lot Redeem lines in the points ledger), cash goes back
    /// to the account and the stock is released.
    /// </summary>
    public async Task<RedemptionDto> CancelAsync(int redemptionId)
    {
        var redemption = await LoadQuery()
            .Include(r => r.Account)
            .FirstOrDefaultAsync(r => r.RedemptionId == redemptionId)
            ?? throw NotFoundException.For("Redemption", redemptionId);

        switch (redemption.Status)
        {
            case RedemptionStatus.Issued:
                break;
            case RedemptionStatus.Used:
                throw new BusinessRuleException("This voucher has already been used and can't be cancelled.");
            case RedemptionStatus.Cancelled:
                throw new BusinessRuleException("This voucher is already cancelled.");
            case RedemptionStatus.Expired:
                throw new BusinessRuleException("This voucher has expired and can't be cancelled.");
        }

        await using var transaction = await OwnedTransaction.BeginAsync(_db);

        await _points.RestoreAsync(redemption);

        if (redemption.CashPaid > 0 && redemption.Account != null)
        {
            var credit = AccountPosting.Credit(redemption.Account, redemption.CashPaid,
                AccountTransactionType.DealCashRefund, redemption.VoucherCode,
                string.Format("Cancelled deal: {0}", redemption.Deal.Title));
            credit.Redemption = redemption;
        }

        redemption.Deal.StockRemaining += 1;
        redemption.Status = RedemptionStatus.Cancelled;
        redemption.CancelledAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return redemption.ToDto();
    }

    /// <summary>
    /// Called when a voucher is scanned at a SpendWell till. All deals are in-store,
    /// so any active store can accept any voucher, once.
    /// </summary>
    public async Task<RedemptionDto> UseVoucherAsync(string voucherCode, UseVoucherDto dto)
    {
        string code = voucherCode.Trim().ToUpperInvariant();

        var redemption = await LoadQuery()
            .FirstOrDefaultAsync(r => r.VoucherCode == code)
            ?? throw NotFoundException.For("Voucher", code);

        var now = DateTime.UtcNow;

        switch (redemption.Status)
        {
            case RedemptionStatus.Issued:
                break;
            case RedemptionStatus.Used:
                throw new BusinessRuleException(string.Format(
                    "Voucher {0} was already used on {1:yyyy-MM-dd HH:mm}.", code, redemption.UsedAt));
            case RedemptionStatus.Cancelled:
                throw new BusinessRuleException(string.Format("Voucher {0} was cancelled.", code));
            case RedemptionStatus.Expired:
                throw new BusinessRuleException(string.Format("Voucher {0} has expired.", code));
        }

        if (redemption.ExpiresAt <= now)
        {
            // Background job hasn't caught it yet; mark it now
            redemption.Status = RedemptionStatus.Expired;
            await _db.SaveChangesAsync();
            throw new BusinessRuleException(string.Format("Voucher {0} has expired.", code));
        }

        bool storeOpen = await _db.Stores.AnyAsync(s => s.StoreId == dto.StoreId && s.IsActive);
        if (!storeOpen)
        {
            throw NotFoundException.For("Store", dto.StoreId);
        }

        // Redemption has no RowVersion, so mark it used with one conditional UPDATE:
        // if two tills scan the same code at once, only the first finds it still Issued.
        int updated = await _db.Redemptions
            .Where(r => r.RedemptionId == redemption.RedemptionId && r.Status == RedemptionStatus.Issued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, RedemptionStatus.Used)
                .SetProperty(r => r.UsedAt, now));

        if (updated == 0)
        {
            throw new ConflictException(string.Format("Voucher {0} was just used at another till.", code));
        }

        redemption.Status = RedemptionStatus.Used;
        redemption.UsedAt = now;

        return redemption.ToDto();
    }

    public async Task<RedemptionDto> GetAsync(int redemptionId)
    {
        var redemption = await LoadQuery().FirstOrDefaultAsync(r => r.RedemptionId == redemptionId)
            ?? throw NotFoundException.For("Redemption", redemptionId);

        return redemption.ToDto();
    }

    public async Task<IReadOnlyList<RedemptionDto>> GetForMemberAsync(int memberId, RedemptionStatus? status)
    {
        bool exists = await _db.Members.AnyAsync(m => m.MemberId == memberId);
        if (!exists)
        {
            throw NotFoundException.For("Member", memberId);
        }

        var query = LoadQuery().Where(r => r.MemberId == memberId);
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var list = await query.OrderByDescending(r => r.IssuedAt).ToListAsync();
        return list.Select(r => r.ToDto()).ToList();
    }

    private IQueryable<Redemption> LoadQuery()
    {
        return _db.Redemptions
            .Include(r => r.Deal);
    }

    /// <summary>Format SW-XXXX-XXXX using an alphabet without look-alike characters.</summary>
    private async Task<string> GenerateUniqueVoucherCodeAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[8];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = VoucherAlphabet[RandomNumberGenerator.GetInt32(VoucherAlphabet.Length)];
            }

            string code = string.Format("SW-{0}-{1}", new string(chars, 0, 4), new string(chars, 4, 4));

            bool taken = await _db.Redemptions.AnyAsync(r => r.VoucherCode == code);
            if (!taken)
            {
                return code;
            }
        }

        throw new ConflictException("Could not generate a unique voucher code. Please try again.");
    }
}
