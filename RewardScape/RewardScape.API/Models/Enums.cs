namespace SpendWell.Api.Models;

public enum AccountType
{
    Cheque,
    Savings
}

public enum AccountStatus
{
    Active,
    Frozen,
    Closed
}

public enum TransactionDirection
{
    Debit,   // money goes OUT of the account
    Credit   // money comes IN to the account
}

public enum AccountTransactionType
{
    Deposit,
    PurchaseDebit,      // till purchases AND online orders
    RefundCredit,
    DealCashPayment,
    DealCashRefund,
    TravelPayment,
    TravelRefund
}

public enum PurchaseStatus
{
    Completed,
    PartiallyRefunded,
    Refunded
}

/// <summary>Where points came from. Lets the member see what each part of their life earned.</summary>
public enum RewardCategory
{
    Shopping,
    Travel,
    Driving,
    Bonus
}

public enum PointsTransactionType
{
    Earn,      // points awarded (shopping milestone, completed trip, driving month)
    Redeem,    // points spent on a deal
    Reverse,   // points clawed back after a refund
    Expire,    // points that passed their expiry date
    Restore,   // points returned after a cancelled redemption
    Bonus      // manual/admin bonus
}

public enum RedemptionStatus
{
    Issued,
    Used,
    Cancelled,
    Expired
}

public enum SettlementStatus
{
    Pending,
    Paid
}

public enum ProductCategory
{
    Groceries,
    FreshProduce,
    Clothing,
    Electronics,
    Homeware,
    Health,
    Delivery,   // the delivery fee line on an online order
    Other
}

public enum PartnerCategory
{
    Coffee,
    Cinema,
    Fuel,
    Restaurant,
    Travel,
    Other
}

public enum CartStatus
{
    Active,
    CheckedOut,
    Abandoned
}

public enum OrderStatus
{
    Paid,
    Packed,
    Shipped,
    Delivered,
    Cancelled
}

public enum TravelType
{
    Flight,
    Hotel,
    CarHire
}

public enum TravelBookingStatus
{
    Confirmed,   // paid, trip not finished yet (points pending)
    Cancelled,
    Completed    // trip finished, points awarded
}
