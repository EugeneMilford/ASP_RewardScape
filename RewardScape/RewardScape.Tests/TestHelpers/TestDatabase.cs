using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SpendWell.Api.Data;

namespace SpendWell.Tests.TestHelpers;

/// <summary>
/// The real DbContext with one change for SQLite: SQLite has no rowversion column type,
/// so RowVersion is turned into a plain column. Concurrency (409) is tested against
/// real SQL Server in the integration tests instead.
/// </summary>
public class TestSpendWellDbContext : SpendWellDbContext
{
    public TestSpendWellDbContext(DbContextOptions<SpendWellDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty("RowVersion");
            if (rowVersion != null)
            {
                rowVersion.ValueGenerated = ValueGenerated.Never;
                rowVersion.IsConcurrencyToken = false;
            }
        }
    }
}

/// <summary>
/// A fresh in-memory SQLite database per test. SQLite is used (not the EF InMemory provider)
/// because the services use real transactions, unique indexes and foreign keys.
/// The database lives as long as the connection, so every context made from one
/// TestDatabase sees the same data.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SpendWellDbContext> _options;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<SpendWellDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = NewContext();
        db.Database.EnsureCreated();   // also applies HasData seeds: tiers, milestones, stores, travel, bands
    }

    /// <summary>
    /// A new context on the same database. Use a separate one to check what was really
    /// saved, instead of what the service's context still has in memory.
    /// </summary>
    public SpendWellDbContext NewContext()
    {
        return new TestSpendWellDbContext(_options);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
