namespace SpendWell.Api.Models;

/// <summary>
/// The bank-style account linked to a member. Every purchase debits this account,
/// every refund credits it, so the member can see exactly how much money goes out per spend.
/// </summary>
public class Account
{
    public int AccountId { get; set; }

    /// <summary>10-digit account number, unique across the system.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    public AccountType AccountType { get; set; } = AccountType.Cheque;
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public decimal Balance { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;

    public ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();

    // Concurrency token: two purchases at once can't both spend the same rand
    [System.ComponentModel.DataAnnotations.Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
