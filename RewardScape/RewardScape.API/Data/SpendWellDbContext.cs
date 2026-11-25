using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Models;

namespace SpendWell.Api.Data;

public class SpendWellDbContext : DbContext
{
    public SpendWellDbContext(DbContextOptions<SpendWellDbContext> options) : base(options)
    {
    }

    // Members, accounts, points
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Tier> Tiers => Set<Tier>();
    public DbSet<TierHistory> TierHistories => Set<TierHistory>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();
    public DbSet<PointsLot> PointsLots => Set<PointsLot>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();

    // In-store spending and milestones
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<SpendMilestone> SpendMilestones => Set<SpendMilestone>();

    // Deals (redeemed at SpendWell tills)
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<Redemption> Redemptions => Set<Redemption>();

    // Online shopping
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // Travel
    public DbSet<TravelProvider> TravelProviders => Set<TravelProvider>();
    public DbSet<TravelListing> TravelListings => Set<TravelListing>();
    public DbSet<TravelBooking> TravelBookings => Set<TravelBooking>();
    public DbSet<TravelTraveller> TravelTravellers => Set<TravelTraveller>();
    public DbSet<TravelEarnRule> TravelEarnRules => Set<TravelEarnRule>();

    // Driving
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<DrivingTrip> DrivingTrips => Set<DrivingTrip>();
    public DbSet<DrivingMonthlySummary> DrivingMonthlySummaries => Set<DrivingMonthlySummary>();
    public DbSet<DrivingRewardBand> DrivingRewardBands => Set<DrivingRewardBand>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureCore(modelBuilder);
        ConfigureShopping(modelBuilder);
        ConfigureTravel(modelBuilder);
        ConfigureDriving(modelBuilder);

        // Financial records must never disappear because a parent row was deleted.
        // Restrict on every relationship also avoids SQL Server's "multiple cascade paths" error.
        // Ownership links (Order -> Address) are part of the same row, so they're left alone.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(e => e.GetForeignKeys())
                     .Where(fk => !fk.IsOwnership))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }

        SeedData.Apply(modelBuilder);
    }

    private static void ConfigureCore(ModelBuilder modelBuilder)
    {
        // Money and multipliers: never let SQL Server guess the precision
        modelBuilder.Entity<Account>().Property(a => a.Balance).HasPrecision(18, 2);
        modelBuilder.Entity<AccountTransaction>().Property(t => t.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<AccountTransaction>().Property(t => t.BalanceAfter).HasPrecision(18, 2);
        modelBuilder.Entity<Purchase>().Property(p => p.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<Purchase>().Property(p => p.RefundedAmount).HasPrecision(18, 2);
        modelBuilder.Entity<PurchaseItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<PurchaseItem>().Property(i => i.LineTotal).HasPrecision(18, 2);
        modelBuilder.Entity<Refund>().Property(r => r.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<SpendMilestone>().Property(m => m.ThresholdAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Tier>().Property(t => t.EarnMultiplier).HasPrecision(5, 2);
        modelBuilder.Entity<Tier>().Property(t => t.DealDiscountPercent).HasPrecision(5, 2);
        modelBuilder.Entity<Tier>().Property(t => t.TravelDiscountPercent).HasPrecision(5, 2);
        modelBuilder.Entity<PointsTransaction>().Property(t => t.MultiplierApplied).HasPrecision(5, 2);
        modelBuilder.Entity<Deal>().Property(d => d.CashPrice).HasPrecision(18, 2);
        modelBuilder.Entity<Redemption>().Property(r => r.CashPaid).HasPrecision(18, 2);

        // Enums stored as readable text
        modelBuilder.Entity<Account>().Property(a => a.AccountType).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Account>().Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<AccountTransaction>().Property(t => t.Type).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<AccountTransaction>().Property(t => t.Direction).HasConversion<string>().HasMaxLength(10);
        modelBuilder.Entity<Purchase>().Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<PurchaseItem>().Property(i => i.Category).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<PointsTransaction>().Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<PointsTransaction>().Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Redemption>().Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // Unique rules the business depends on
        modelBuilder.Entity<Member>().HasIndex(m => m.Email).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(a => a.AccountNumber).IsUnique();
        modelBuilder.Entity<Account>().Property(a => a.AccountNumber).HasMaxLength(10).IsFixedLength();
        modelBuilder.Entity<Purchase>().HasIndex(p => p.ExternalReference).IsUnique();
        modelBuilder.Entity<Redemption>().HasIndex(r => r.VoucherCode).IsUnique();
        modelBuilder.Entity<Tier>().HasIndex(t => t.Rank).IsUnique();

        // Indexes for the queries that run most
        modelBuilder.Entity<AccountTransaction>().HasIndex(t => new { t.AccountId, t.CreatedAt });
        modelBuilder.Entity<Purchase>().HasIndex(p => new { p.MemberId, p.PurchasedAt });
        modelBuilder.Entity<PointsLot>().HasIndex(l => new { l.MemberId, l.ExpiresAt });
        modelBuilder.Entity<PointsTransaction>().HasIndex(t => new { t.MemberId, t.CreatedAt });
        modelBuilder.Entity<PointsTransaction>().HasIndex(t => new { t.MemberId, t.Category, t.CreatedAt });

        // TierHistory has two FKs to Tier
        modelBuilder.Entity<TierHistory>()
            .HasOne(h => h.FromTier).WithMany().HasForeignKey(h => h.FromTierId);
        modelBuilder.Entity<TierHistory>()
            .HasOne(h => h.ToTier).WithMany().HasForeignKey(h => h.ToTierId);

        modelBuilder.Entity<Deal>()
            .HasOne(d => d.MinTier).WithMany().HasForeignKey(d => d.MinTierId);

        modelBuilder.Entity<Member>()
            .HasOne(m => m.Tier).WithMany(t => t.Members).HasForeignKey(m => m.TierId);
    }

    private static void ConfigureShopping(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Product>().Property(p => p.Category).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<Product>().Property(p => p.Sku).HasMaxLength(30);
        modelBuilder.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(p => new { p.Category, p.IsActive });

        modelBuilder.Entity<Cart>().Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        // Only ONE active cart per member; old checked-out carts don't count
        modelBuilder.Entity<Cart>()
            .HasIndex(c => c.MemberId)
            .IsUnique()
            .HasFilter("[Status] = 'Active'");

        modelBuilder.Entity<CartItem>().HasIndex(i => new { i.CartId, i.ProductId }).IsUnique();

        modelBuilder.Entity<Order>().Property(o => o.Subtotal).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.DeliveryFee).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
        modelBuilder.Entity<Order>().Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();
        modelBuilder.Entity<Order>().HasIndex(o => new { o.MemberId, o.PlacedAt });

        modelBuilder.Entity<Order>().OwnsOne(o => o.DeliveryAddress, address =>
        {
            address.Property(a => a.Line1).HasMaxLength(150).HasColumnName("DeliveryLine1");
            address.Property(a => a.Line2).HasMaxLength(150).HasColumnName("DeliveryLine2");
            address.Property(a => a.Suburb).HasMaxLength(100).HasColumnName("DeliverySuburb");
            address.Property(a => a.City).HasMaxLength(100).HasColumnName("DeliveryCity");
            address.Property(a => a.PostalCode).HasMaxLength(4).HasColumnName("DeliveryPostalCode");
        });

        modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.LineTotal).HasPrecision(18, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.Category).HasConversion<string>().HasMaxLength(30);
    }

    private static void ConfigureTravel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TravelProvider>().Property(p => p.Type).HasConversion<string>().HasMaxLength(20);

        modelBuilder.Entity<TravelListing>().Property(l => l.Type).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<TravelListing>().Property(l => l.PricePerUnit).HasPrecision(18, 2);
        modelBuilder.Entity<TravelListing>().HasIndex(l => new { l.Type, l.Destination, l.AvailableFrom });

        modelBuilder.Entity<TravelBooking>().Property(b => b.Type).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<TravelBooking>().Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<TravelBooking>().Property(b => b.BaseAmount).HasPrecision(18, 2);
        modelBuilder.Entity<TravelBooking>().Property(b => b.TierDiscountPercent).HasPrecision(5, 2);
        modelBuilder.Entity<TravelBooking>().Property(b => b.DiscountAmount).HasPrecision(18, 2);
        modelBuilder.Entity<TravelBooking>().Property(b => b.TotalPaid).HasPrecision(18, 2);
        modelBuilder.Entity<TravelBooking>().Property(b => b.CancellationFee).HasPrecision(18, 2);
        modelBuilder.Entity<TravelBooking>().Property(b => b.RefundedAmount).HasPrecision(18, 2);
        modelBuilder.Entity<TravelBooking>().HasIndex(b => b.BookingReference).IsUnique();

        // Availability check: "bookings on this listing that overlap these dates"
        modelBuilder.Entity<TravelBooking>().HasIndex(b => new { b.ListingId, b.StartDate, b.EndDate });

        // Completion job: "confirmed bookings whose trip has ended"
        modelBuilder.Entity<TravelBooking>().HasIndex(b => new { b.Status, b.EndDate });

        //modelBuilder.Entity<TravelTraveller>()
        //    .HasOne(t => t.Booking).WithMany(b => b.Travellers).HasForeignKey(t => t.BookingId);

        modelBuilder.Entity<TravelEarnRule>().Property(r => r.Type).HasConversion<string>().HasMaxLength(20);
        modelBuilder.Entity<TravelEarnRule>().Property(r => r.PointsPerR10).HasPrecision(5, 2);
        modelBuilder.Entity<TravelEarnRule>().HasIndex(r => r.Type).IsUnique();
    }

    private static void ConfigureDriving(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vehicle>().Property(v => v.RegistrationNumber).HasMaxLength(15);
        modelBuilder.Entity<Vehicle>().Property(v => v.TelematicsDeviceId).HasMaxLength(50);
        modelBuilder.Entity<Vehicle>().HasIndex(v => v.RegistrationNumber).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(v => v.TelematicsDeviceId).IsUnique();

        modelBuilder.Entity<DrivingTrip>().Property(t => t.DistanceKm).HasPrecision(10, 2);
        modelBuilder.Entity<DrivingTrip>().Property(t => t.Score).HasPrecision(5, 1);
        modelBuilder.Entity<DrivingTrip>().Property(t => t.ExternalTripId).HasMaxLength(64);
        modelBuilder.Entity<DrivingTrip>().HasIndex(t => t.ExternalTripId).IsUnique();
        modelBuilder.Entity<DrivingTrip>().HasIndex(t => new { t.MemberId, t.StartedAt });

        modelBuilder.Entity<DrivingMonthlySummary>().Property(s => s.TotalDistanceKm).HasPrecision(10, 2);
        modelBuilder.Entity<DrivingMonthlySummary>().Property(s => s.AverageScore).HasPrecision(5, 1);
        modelBuilder.Entity<DrivingMonthlySummary>().Property(s => s.MultiplierApplied).HasPrecision(5, 2);
        modelBuilder.Entity<DrivingMonthlySummary>().HasIndex(s => new { s.MemberId, s.Year, s.Month }).IsUnique();

        modelBuilder.Entity<DrivingRewardBand>().Property(b => b.MinScore).HasPrecision(5, 1);
        modelBuilder.Entity<DrivingRewardBand>().HasIndex(b => b.MinScore).IsUnique();
    }
}
