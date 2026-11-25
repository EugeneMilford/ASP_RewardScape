using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SpendWell.Api.Common;
using SpendWell.Api.Data;
using SpendWell.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SpendWellDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RewardScape")));

// Core
builder.Services.AddScoped<IPointsService, PointsService>();
builder.Services.AddScoped<ITierService, TierService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IMemberService, MemberService>();

// Shopping
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// Travel and driving
builder.Services.AddScoped<ITravelService, TravelService>();
builder.Services.AddScoped<IDrivingService, DrivingService>();

// Deals and vouchers
builder.Services.AddScoped<IDealService, DealService>();
builder.Services.AddScoped<IRedemptionService, RedemptionService>();

// Scheduled jobs
builder.Services.AddScoped<IExpiryService, ExpiryService>();
builder.Services.AddHostedService<RewardsBackgroundService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SpendWellDbContext>();
    if (db.Database.GetMigrations().Any())
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }
    await SeedData.SeedRuntimeDataAsync(db);
}

app.UseExceptionHandler();

// Swagger on in every environment so the live Azure site shows it too
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

public partial class Program
{
}