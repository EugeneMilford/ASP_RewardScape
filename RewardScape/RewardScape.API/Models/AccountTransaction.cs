namespace SpendWell.Api.Models;

/// <summary>
/// One line on the account statement. Amount is always positive; Direction says whether
/// the money went out (Debit) or came in (Credit).
/// </summary>
public class AccountTransaction
{
    public int AccountTransactionId { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public AccountTransactionType Type { get; set; }
    public TransactionDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }

    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Links back to what caused the money movement (only one is set)
    public int? PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public int? RefundId { get; set; }
    public Refund? Refund { get; set; }

    public int? RedemptionId { get; set; }
    public Redemption? Redemption { get; set; }

    public int? TravelBookingId { get; set; }
    public TravelBooking? TravelBooking { get; set; }
}
