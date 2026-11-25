using SpendWell.Api.Common;
using SpendWell.Api.Models;

namespace SpendWell.Api.Services;

/// <summary>
/// The only place money moves in or out of an account. Every debit and credit changes the
/// balance AND writes a statement line with the balance after, so the two can never disagree.
/// Does not call SaveChanges; the calling service saves inside its own transaction.
/// </summary>
public static class AccountPosting
{
    public static AccountTransaction Debit(
        Account account,
        decimal amount,
        AccountTransactionType type,
        string reference,
        string description)
    {
        EnsureUsable(account);

        if (amount <= 0)
        {
            throw new BusinessRuleException("Debit amount must be greater than zero.");
        }

        if (account.Balance < amount)
        {
            throw new BusinessRuleException(string.Format(
                "Insufficient funds in account {0}. Balance is R{1:N2}, R{2:N2} is needed.",
                account.AccountNumber, account.Balance, amount));
        }

        account.Balance -= amount;

        var transaction = new AccountTransaction
        {
            Account = account,
            Type = type,
            Direction = TransactionDirection.Debit,
            Amount = amount,
            BalanceAfter = account.Balance,
            Reference = reference,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        account.Transactions.Add(transaction);
        return transaction;
    }

    public static AccountTransaction Credit(
        Account account,
        decimal amount,
        AccountTransactionType type,
        string reference,
        string description)
    {
        if (account.Status == AccountStatus.Closed)
        {
            throw new BusinessRuleException(string.Format("Account {0} is closed.", account.AccountNumber));
        }

        if (amount <= 0)
        {
            throw new BusinessRuleException("Credit amount must be greater than zero.");
        }

        account.Balance += amount;

        var transaction = new AccountTransaction
        {
            Account = account,
            Type = type,
            Direction = TransactionDirection.Credit,
            Amount = amount,
            BalanceAfter = account.Balance,
            Reference = reference,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        account.Transactions.Add(transaction);
        return transaction;
    }

    public static void EnsureUsable(Account account)
    {
        switch (account.Status)
        {
            case AccountStatus.Active:
                return;
            case AccountStatus.Frozen:
                throw new BusinessRuleException(string.Format("Account {0} is frozen.", account.AccountNumber));
            case AccountStatus.Closed:
                throw new BusinessRuleException(string.Format("Account {0} is closed.", account.AccountNumber));
            default:
                throw new BusinessRuleException("Account status is not valid.");
        }
    }
}
