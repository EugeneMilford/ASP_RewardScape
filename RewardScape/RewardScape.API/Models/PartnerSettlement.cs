namespace SpendWell.Api.Models;

/// <summary>
/// What SpendWell owes one partner for one calendar month of used vouchers.
/// </summary>
public class PartnerSettlement
{
    public int PartnerSettlementId { get; set; }

    public int PartnerId { get; set; }
    public Partner Partner { get; set; } = null!;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public int VouchersUsed { get; set; }
    public decimal AmountOwed { get; set; }

    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
    public DateTime? PaidAt { get; set; }
}
