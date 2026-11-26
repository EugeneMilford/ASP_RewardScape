using System.Text.Json;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SpendWell.Api.Data;
using SpendWell.Api.Services;
using Testcontainers.MsSql;

namespace SpendWell.Tests.Integration;

/// <summary>
/// Starts the real API in memory against a real SQL Server:
/// - Docker available (e.g. GitHub Actions): SQL Server in a container
/// - No Docker on Windows: LocalDB with a throwaway database, deleted afterwards
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer? _sql;
    private string _connectionString = string.Empty;
    private bool _usingLocalDb;

    public static readonly JsonSerializerOptions Json = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public async Task InitializeAsync()
    {
        if (IsDockerAvailable())
        {
            _sql = new MsSqlBuilder().Build();
            await _sql.StartAsync();
            _connectionString = _sql.GetConnectionString();
        }
        else if (OperatingSystem.IsWindows())
        {
            _usingLocalDb = true;
            _connectionString = string.Format(
                "Server=(localdb)\\mssqllocaldb;Database=RewardScapeTests_{0};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
                Guid.NewGuid().ToString("N"));
        }
        else
        {
            throw new InvalidOperationException("The integration tests need Docker, or LocalDB on Windows.");
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_sql != null)
        {
            await _sql.DisposeAsync();
        }

        if (_usingLocalDb)
        {
            // Remove the throwaway test database
            var options = new DbContextOptionsBuilder<SpendWellDbContext>()
                .UseSqlServer(_connectionString)
                .Options;

            using var db = new SpendWellDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            new MsSqlBuilder().Build();   
            return true;
        }
        catch (DockerUnavailableException)
        {
            return false;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // The test database 
            services.RemoveAll<DbContextOptions<SpendWellDbContext>>();
            services.AddDbContext<SpendWellDbContext>(options => options.UseSqlServer(_connectionString));

            // The 6-hourly reward jobs would run at start-up and change data under the tests
            var jobs = services.SingleOrDefault(d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(RewardsBackgroundService));
            if (jobs != null)
            {
                services.Remove(jobs);
            }
        });
    }
}

[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
}
