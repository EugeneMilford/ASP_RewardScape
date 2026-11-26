using SpendWell.Api.Data;

namespace SpendWell.Tests.TestHelpers;

/// <summary>
/// Base class for service tests: every test gets its own empty, seeded database,
/// a context (Db) and the services built on it.
/// </summary>
public abstract class DbTestBase : IDisposable
{
    protected DbTestBase()
    {
        Database = new TestDatabase();
        Db = Database.NewContext();
        Services = new ServiceSet(Db);
    }

    protected TestDatabase Database { get; }
    protected SpendWellDbContext Db { get; }
    protected ServiceSet Services { get; }

    /// <summary>A separate context, to read what was really saved.</summary>
    protected SpendWellDbContext Fresh()
    {
        return Database.NewContext();
    }

    public void Dispose()
    {
        Db.Dispose();
        Database.Dispose();
    }
}
