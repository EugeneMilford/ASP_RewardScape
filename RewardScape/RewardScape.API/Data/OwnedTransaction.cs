using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SpendWell.Api.Data;

/// <summary>
/// Starts a database transaction only if one isn't already running.
/// This lets services call each other: checkout calls the purchase service, and
/// the purchase service's work joins checkout's transaction instead of starting its own.
/// Only the code that actually started the transaction commits it.
/// If CommitAsync is never reached (an exception), disposing rolls everything back.
/// </summary>
public sealed class OwnedTransaction : IAsyncDisposable
{
    private readonly IDbContextTransaction? _transaction;

    private OwnedTransaction(IDbContextTransaction? transaction)
    {
        _transaction = transaction;
    }

    public static async Task<OwnedTransaction> BeginAsync(DbContext db)
    {
        if (db.Database.CurrentTransaction != null)
        {
            return new OwnedTransaction(null);
        }

        var transaction = await db.Database.BeginTransactionAsync();
        return new OwnedTransaction(transaction);
    }

    public async Task CommitAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
        }
    }
}
