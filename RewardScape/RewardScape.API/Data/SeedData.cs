using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Models;

namespace SpendWell.Api.Data;

/// <summary>
/// Reference data the app needs before anyone signs up.
/// </summary>
public static class SeedData
{
    public const int OnlineStoreId = 4;

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tier>().HasData(
            new Tier { TierId = 1, Name = "Blue", Rank = 1, MinPointsRolling12M = 0, EarnMultiplier = 1.00m, DealDiscountPercent = 0m, TravelDiscountPercent = 0m },
            new Tier { TierId = 2, Name = "Silver", Rank = 2, MinPointsRolling12M = 2000, EarnMultiplier = 1.25m, DealDiscountPercent = 5m, TravelDiscountPercent = 5m },
            new Tier { TierId = 3, Name = "Gold", Rank = 3, MinPointsRolling12M = 6000, EarnMultiplier = 1.50m, DealDiscountPercent = 10m, TravelDiscountPercent = 15m },
            new Tier { TierId = 4, Name = "Diamond", Rank = 4, MinPointsRolling12M = 15000, EarnMultiplier = 2.00m, DealDiscountPercent = 20m, TravelDiscountPercent = 25m });

        modelBuilder.Entity<SpendMilestone>().HasData(
            new SpendMilestone { SpendMilestoneId = 1, ThresholdAmount = 500m, CumulativePoints = 100, IsActive = true },
            new SpendMilestone { SpendMilestoneId = 2, ThresholdAmount = 1000m, CumulativePoints = 250, IsActive = true },
            new SpendMilestone { SpendMilestoneId = 3, ThresholdAmount = 2500m, CumulativePoints = 700, IsActive = true },
            new SpendMilestone { SpendMilestoneId = 4, ThresholdAmount = 5000m, CumulativePoints = 1500, IsActive = true });

        modelBuilder.Entity<Store>().HasData(
            new Store { StoreId = 1, Name = "SpendWell Vredenburg", Region = "West Coast", IsActive = true },
            new Store { StoreId = 2, Name = "SpendWell Saldanha", Region = "West Coast", IsActive = true },
            new Store { StoreId = 3, Name = "SpendWell Cape Town CBD", Region = "Cape Metro", IsActive = true },
            new Store { StoreId = OnlineStoreId, Name = "SpendWell Online", Region = "Online", IsActive = true });

        modelBuilder.Entity<TravelProvider>().HasData(
            new TravelProvider { TravelProviderId = 1, Name = "SkyBridge Air", Type = TravelType.Flight, IsActive = true },
            new TravelProvider { TravelProviderId = 2, Name = "Coastal Stays", Type = TravelType.Hotel, IsActive = true },
            new TravelProvider { TravelProviderId = 3, Name = "RoadReady Car Hire", Type = TravelType.CarHire, IsActive = true });

        // Every R10 of travel spend earns this many points (before tier multiplier)
        modelBuilder.Entity<TravelEarnRule>().HasData(
            new TravelEarnRule { TravelEarnRuleId = 1, Type = TravelType.Flight, PointsPerR10 = 2.0m },
            new TravelEarnRule { TravelEarnRuleId = 2, Type = TravelType.Hotel, PointsPerR10 = 3.0m },
            new TravelEarnRule { TravelEarnRuleId = 3, Type = TravelType.CarHire, PointsPerR10 = 1.5m });

        modelBuilder.Entity<DrivingRewardBand>().HasData(
            new DrivingRewardBand { DrivingRewardBandId = 1, Name = "Excellent", MinScore = 90m, BasePoints = 600 },
            new DrivingRewardBand { DrivingRewardBandId = 2, Name = "Good", MinScore = 80m, BasePoints = 400 },
            new DrivingRewardBand { DrivingRewardBandId = 3, Name = "Fair", MinScore = 70m, BasePoints = 200 },
            new DrivingRewardBand { DrivingRewardBandId = 4, Name = "Improving", MinScore = 60m, BasePoints = 50 });
    }

    public static async Task SeedRuntimeDataAsync(SpendWellDbContext db)
    {
        await SeedDealsAsync(db);
        await SeedProductsAsync(db);
        await SeedTravelListingsAsync(db);
    }

    private static async Task SeedDealsAsync(SpendWellDbContext db)
    {
        if (await db.Deals.AnyAsync())
        {
            return;
        }

        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        db.Deals.AddRange(
            new Deal
            {
                Title = "Free medium coffee", Description = "Any medium hot drink from the in-store coffee counter.",
                PointsCost = 150, CashPrice = 0m, MinTierId = 1,
                StockRemaining = 500, MaxPerMember = 4, VoucherValidDays = 30,
                StartsAt = start, EndsAt = end, IsActive = true
            },
            new Deal
            {
                Title = "R100 off a R500 shop", Description = "R100 off any in-store shop of R500 or more. Pay R20 top-up.",
                PointsCost = 400, CashPrice = 20m, MinTierId = 2,
                StockRemaining = 200, MaxPerMember = 2, VoucherValidDays = 60,
                StartsAt = start, EndsAt = end, IsActive = true
            },
            new Deal
            {
                Title = "Free 1.7L kettle", Description = "Collect a cordless kettle at any SpendWell store.",
                PointsCost = 800, CashPrice = 0m, MinTierId = 3,
                StockRemaining = 100, MaxPerMember = 1, VoucherValidDays = 30,
                StartsAt = start, EndsAt = end, IsActive = true
            },
            new Deal
            {
                Title = "25% off fresh produce basket", Description = "R150 fresh produce basket for R112.50.",
                PointsCost = 250, CashPrice = 112.50m, MinTierId = 1,
                StockRemaining = 300, MaxPerMember = 2, VoucherValidDays = 14,
                StartsAt = start, EndsAt = end, IsActive = true
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedProductsAsync(SpendWellDbContext db)
    {
        if (await db.Products.AnyAsync())
        {
            return;
        }

        db.Products.AddRange(
            NewProduct("GRC-MILK-2L", "Full cream milk 2L", ProductCategory.Groceries, 32.99m, 200),
            NewProduct("GRC-BREAD-WW", "Whole wheat bread 700g", ProductCategory.Groceries, 19.99m, 150),
            NewProduct("GRC-RICE-2KG", "Basmati rice 2kg", ProductCategory.Groceries, 64.99m, 80),
            NewProduct("GRC-COFFEE-250", "Filter coffee 250g", ProductCategory.Groceries, 89.99m, 60),
            NewProduct("FRP-APPLE-1.5", "Apples 1.5kg bag", ProductCategory.FreshProduce, 34.99m, 120),
            NewProduct("FRP-AVO-4", "Avocados x4", ProductCategory.FreshProduce, 44.99m, 90),
            NewProduct("CLT-TEE-M", "Cotton T-shirt (M)", ProductCategory.Clothing, 129.00m, 40),
            NewProduct("ELC-EARBUDS", "Wireless earbuds", ProductCategory.Electronics, 599.00m, 25),
            NewProduct("HOM-KETTLE", "1.7L cordless kettle", ProductCategory.Homeware, 349.00m, 30),
            NewProduct("HLT-VITC-60", "Vitamin C 60 tablets", ProductCategory.Health, 79.99m, 70));

        await db.SaveChangesAsync();
    }

    private static Product NewProduct(string sku, string name, ProductCategory category, decimal price, int stock)
    {
        return new Product
        {
            Sku = sku,
            Name = name,
            Description = name,
            Category = category,
            Price = price,
            StockQuantity = stock,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>Travel dates are relative to today so the demo data is always bookable.</summary>
    private static async Task SeedTravelListingsAsync(SpendWellDbContext db)
    {
        if (await db.TravelListings.AnyAsync())
        {
            return;
        }

        var today = DateTime.UtcNow.Date;

        db.TravelListings.AddRange(
            new TravelListing
            {
                ProviderId = 1, Type = TravelType.Flight,
                Title = "SB204 Cape Town to Johannesburg", Description = "Economy, 1 x 23kg bag included.",
                Origin = "Cape Town", Destination = "Johannesburg",
                AvailableFrom = today.AddDays(30).AddHours(7), AvailableTo = today.AddDays(30).AddHours(9),
                PricePerUnit = 1450m, TotalUnits = 120, MaxUnitsPerBooking = 6, IsActive = true
            },
            new TravelListing
            {
                ProviderId = 1, Type = TravelType.Flight,
                Title = "SB311 Cape Town to Durban", Description = "Economy, carry-on only.",
                Origin = "Cape Town", Destination = "Durban",
                AvailableFrom = today.AddDays(45).AddHours(13), AvailableTo = today.AddDays(45).AddHours(15),
                PricePerUnit = 1290m, TotalUnits = 150, MaxUnitsPerBooking = 6, IsActive = true
            },
            new TravelListing
            {
                ProviderId = 2, Type = TravelType.Hotel,
                Title = "Coastal Stays Knysna - Standard room", Description = "Sleeps 2, breakfast included.",
                Origin = null, Destination = "Knysna",
                AvailableFrom = today.AddDays(1), AvailableTo = today.AddDays(365),
                PricePerUnit = 1200m, TotalUnits = 10, MaxUnitsPerBooking = 3, IsActive = true
            },
            new TravelListing
            {
                ProviderId = 2, Type = TravelType.Hotel,
                Title = "Coastal Stays Langebaan - Sea view suite", Description = "Sleeps 4, self-catering.",
                Origin = null, Destination = "Langebaan",
                AvailableFrom = today.AddDays(1), AvailableTo = today.AddDays(365),
                PricePerUnit = 2100m, TotalUnits = 4, MaxUnitsPerBooking = 2, IsActive = true
            },
            new TravelListing
            {
                ProviderId = 3, Type = TravelType.CarHire,
                Title = "Compact car - Cape Town Airport", Description = "Manual, unlimited km.",
                Origin = null, Destination = "Cape Town",
                AvailableFrom = today.AddDays(1), AvailableTo = today.AddDays(365),
                PricePerUnit = 450m, TotalUnits = 15, MaxUnitsPerBooking = 2, IsActive = true
            });

        await db.SaveChangesAsync();
    }
}
