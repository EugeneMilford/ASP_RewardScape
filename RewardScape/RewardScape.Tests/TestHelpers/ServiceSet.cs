using Microsoft.Extensions.Logging.Abstractions;
using SpendWell.Api.Data;
using SpendWell.Api.Services;

namespace SpendWell.Tests.TestHelpers;

/// <summary>All the real services wired together on one DbContext, the way DI does it in Program.cs.</summary>
public sealed class ServiceSet
{
    public ServiceSet(SpendWellDbContext db)
    {
        Points = new PointsService(db);
        Tiers = new TierService(db, Points);
        Accounts = new AccountService(db);
        Members = new MemberService(db, Accounts, Points);
        Purchases = new PurchaseService(db, Points, Tiers);
        Deals = new DealService(db, Points);
        Redemptions = new RedemptionService(db, Points, Deals);
        Products = new ProductService(db);
        Carts = new CartService(db);
        Orders = new OrderService(db, Purchases);
        Travel = new TravelService(db, Points, Tiers, NullLogger<TravelService>.Instance);
        Driving = new DrivingService(db, Points, Tiers, NullLogger<DrivingService>.Instance);
        Expiry = new ExpiryService(db);
    }

    public PointsService Points { get; }
    public TierService Tiers { get; }
    public AccountService Accounts { get; }
    public MemberService Members { get; }
    public PurchaseService Purchases { get; }
    public DealService Deals { get; }
    public RedemptionService Redemptions { get; }
    public ProductService Products { get; }
    public CartService Carts { get; }
    public OrderService Orders { get; }
    public TravelService Travel { get; }
    public DrivingService Driving { get; }
    public ExpiryService Expiry { get; }
}
