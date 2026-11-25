using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RewardScape.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DrivingRewardBands",
                columns: table => new
                {
                    DrivingRewardBandId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinScore = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
                    BasePoints = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrivingRewardBands", x => x.DrivingRewardBandId);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sku = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "SpendMilestones",
                columns: table => new
                {
                    SpendMilestoneId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ThresholdAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CumulativePoints = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendMilestones", x => x.SpendMilestoneId);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    StoreId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.StoreId);
                });

            migrationBuilder.CreateTable(
                name: "Tiers",
                columns: table => new
                {
                    TierId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    MinPointsRolling12M = table.Column<int>(type: "int", nullable: false),
                    EarnMultiplier = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DealDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TravelDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tiers", x => x.TierId);
                });

            migrationBuilder.CreateTable(
                name: "TravelEarnRules",
                columns: table => new
                {
                    TravelEarnRuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PointsPerR10 = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelEarnRules", x => x.TravelEarnRuleId);
                });

            migrationBuilder.CreateTable(
                name: "TravelProviders",
                columns: table => new
                {
                    TravelProviderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelProviders", x => x.TravelProviderId);
                });

            migrationBuilder.CreateTable(
                name: "Deals",
                columns: table => new
                {
                    DealId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PointsCost = table.Column<int>(type: "int", nullable: false),
                    CashPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinTierId = table.Column<int>(type: "int", nullable: false),
                    StockRemaining = table.Column<int>(type: "int", nullable: false),
                    MaxPerMember = table.Column<int>(type: "int", nullable: false),
                    VoucherValidDays = table.Column<int>(type: "int", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deals", x => x.DealId);
                    table.ForeignKey(
                        name: "FK_Deals_Tiers_MinTierId",
                        column: x => x.MinTierId,
                        principalTable: "Tiers",
                        principalColumn: "TierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    MemberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TierId = table.Column<int>(type: "int", nullable: false),
                    PointsOwed = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.MemberId);
                    table.ForeignKey(
                        name: "FK_Members_Tiers_TierId",
                        column: x => x.TierId,
                        principalTable: "Tiers",
                        principalColumn: "TierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TravelListings",
                columns: table => new
                {
                    TravelListingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailableTo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    MaxUnitsPerBooking = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastBookedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelListings", x => x.TravelListingId);
                    table.ForeignKey(
                        name: "FK_TravelListings_TravelProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "TravelProviders",
                        principalColumn: "TravelProviderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountNumber = table.Column<string>(type: "nchar(10)", fixedLength: true, maxLength: 10, nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.AccountId);
                    table.ForeignKey(
                        name: "FK_Accounts_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    CartId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckedOutAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.CartId);
                    table.ForeignKey(
                        name: "FK_Carts_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrivingMonthlySummaries",
                columns: table => new
                {
                    DrivingMonthlySummaryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    TripCount = table.Column<int>(type: "int", nullable: false),
                    TotalDistanceKm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AverageScore = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
                    BasePoints = table.Column<int>(type: "int", nullable: false),
                    MultiplierApplied = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PointsAwarded = table.Column<int>(type: "int", nullable: false),
                    IsFinalised = table.Column<bool>(type: "bit", nullable: false),
                    FinalisedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrivingMonthlySummaries", x => x.DrivingMonthlySummaryId);
                    table.ForeignKey(
                        name: "FK_DrivingMonthlySummaries_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointsLots",
                columns: table => new
                {
                    PointsLotId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    OriginalPoints = table.Column<int>(type: "int", nullable: false),
                    RemainingPoints = table.Column<int>(type: "int", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsExpired = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsLots", x => x.PointsLotId);
                    table.ForeignKey(
                        name: "FK_PointsLots_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointsTransactions",
                columns: table => new
                {
                    PointsTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    MultiplierApplied = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsTransactions", x => x.PointsTransactionId);
                    table.ForeignKey(
                        name: "FK_PointsTransactions_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TierHistories",
                columns: table => new
                {
                    TierHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    FromTierId = table.Column<int>(type: "int", nullable: false),
                    ToTierId = table.Column<int>(type: "int", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TierHistories", x => x.TierHistoryId);
                    table.ForeignKey(
                        name: "FK_TierHistories_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TierHistories_Tiers_FromTierId",
                        column: x => x.FromTierId,
                        principalTable: "Tiers",
                        principalColumn: "TierId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TierHistories_Tiers_ToTierId",
                        column: x => x.ToTierId,
                        principalTable: "Tiers",
                        principalColumn: "TierId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    VehicleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Make = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    TelematicsDeviceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.VehicleId);
                    table.ForeignKey(
                        name: "FK_Vehicles_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Purchases",
                columns: table => new
                {
                    PurchaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalReference = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PointsEarned = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.PurchaseId);
                    table.ForeignKey(
                        name: "FK_Purchases_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Purchases_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Purchases_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "StoreId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Redemptions",
                columns: table => new
                {
                    RedemptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    DealId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: true),
                    PointsSpent = table.Column<int>(type: "int", nullable: false),
                    CashPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VoucherCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Redemptions", x => x.RedemptionId);
                    table.ForeignKey(
                        name: "FK_Redemptions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Redemptions_Deals_DealId",
                        column: x => x.DealId,
                        principalTable: "Deals",
                        principalColumn: "DealId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Redemptions_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TravelBookings",
                columns: table => new
                {
                    TravelBookingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingReference = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Units = table.Column<int>(type: "int", nullable: false),
                    Nights = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TierDiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CancellationFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PointsAwarded = table.Column<int>(type: "int", nullable: false),
                    BookedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelBookings", x => x.TravelBookingId);
                    table.ForeignKey(
                        name: "FK_TravelBookings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TravelBookings_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TravelBookings_TravelListings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "TravelListings",
                        principalColumn: "TravelListingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                columns: table => new
                {
                    CartItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.CartItemId);
                    table.ForeignKey(
                        name: "FK_CartItems_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "CartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CartItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrivingTrips",
                columns: table => new
                {
                    DrivingTripId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalTripId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DistanceKm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    HarshBrakingCount = table.Column<int>(type: "int", nullable: false),
                    HarshAccelerationCount = table.Column<int>(type: "int", nullable: false),
                    HarshCorneringCount = table.Column<int>(type: "int", nullable: false),
                    SpeedingSeconds = table.Column<int>(type: "int", nullable: false),
                    PhoneUseSeconds = table.Column<int>(type: "int", nullable: false),
                    NightDrivingMinutes = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrivingTrips", x => x.DrivingTripId);
                    table.ForeignKey(
                        name: "FK_DrivingTrips_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrivingTrips_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MemberId = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    CartId = table.Column<int>(type: "int", nullable: true),
                    PurchaseId = table.Column<int>(type: "int", nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DeliveryLine1 = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DeliveryLine2 = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DeliverySuburb = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeliveryCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeliveryPostalCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    PlacedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_Orders_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "CartId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseItems",
                columns: table => new
                {
                    PurchaseItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseItems", x => x.PurchaseItemId);
                    table.ForeignKey(
                        name: "FK_PurchaseItems_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    RefundId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RefundedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PointsReversed = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.RefundId);
                    table.ForeignKey(
                        name: "FK_Refunds_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TravelTravellers",
                columns: table => new
                {
                    TravelTravellerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TravelBookingId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsChild = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelTravellers", x => x.TravelTravellerId);
                    table.ForeignKey(
                        name: "FK_TravelTravellers_TravelBookings_TravelBookingId",
                        column: x => x.TravelBookingId,
                        principalTable: "TravelBookings",
                        principalColumn: "TravelBookingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    OrderItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.OrderItemId);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTransactions",
                columns: table => new
                {
                    AccountTransactionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PurchaseId = table.Column<int>(type: "int", nullable: true),
                    RefundId = table.Column<int>(type: "int", nullable: true),
                    RedemptionId = table.Column<int>(type: "int", nullable: true),
                    TravelBookingId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTransactions", x => x.AccountTransactionId);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Redemptions_RedemptionId",
                        column: x => x.RedemptionId,
                        principalTable: "Redemptions",
                        principalColumn: "RedemptionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "RefundId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_TravelBookings_TravelBookingId",
                        column: x => x.TravelBookingId,
                        principalTable: "TravelBookings",
                        principalColumn: "TravelBookingId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "DrivingRewardBands",
                columns: new[] { "DrivingRewardBandId", "BasePoints", "MinScore", "Name" },
                values: new object[,]
                {
                    { 1, 600, 90m, "Excellent" },
                    { 2, 400, 80m, "Good" },
                    { 3, 200, 70m, "Fair" },
                    { 4, 50, 60m, "Improving" }
                });

            migrationBuilder.InsertData(
                table: "SpendMilestones",
                columns: new[] { "SpendMilestoneId", "CumulativePoints", "IsActive", "ThresholdAmount" },
                values: new object[,]
                {
                    { 1, 100, true, 500m },
                    { 2, 250, true, 1000m },
                    { 3, 700, true, 2500m },
                    { 4, 1500, true, 5000m }
                });

            migrationBuilder.InsertData(
                table: "Stores",
                columns: new[] { "StoreId", "IsActive", "Name", "Region" },
                values: new object[,]
                {
                    { 1, true, "SpendWell Vredenburg", "West Coast" },
                    { 2, true, "SpendWell Saldanha", "West Coast" },
                    { 3, true, "SpendWell Cape Town CBD", "Cape Metro" },
                    { 4, true, "SpendWell Online", "Online" }
                });

            migrationBuilder.InsertData(
                table: "Tiers",
                columns: new[] { "TierId", "DealDiscountPercent", "EarnMultiplier", "MinPointsRolling12M", "Name", "Rank", "TravelDiscountPercent" },
                values: new object[,]
                {
                    { 1, 0m, 1.00m, 0, "Blue", 1, 0m },
                    { 2, 5m, 1.25m, 2000, "Silver", 2, 5m },
                    { 3, 10m, 1.50m, 6000, "Gold", 3, 15m },
                    { 4, 20m, 2.00m, 15000, "Diamond", 4, 25m }
                });

            migrationBuilder.InsertData(
                table: "TravelEarnRules",
                columns: new[] { "TravelEarnRuleId", "PointsPerR10", "Type" },
                values: new object[,]
                {
                    { 1, 2.0m, "Flight" },
                    { 2, 3.0m, "Hotel" },
                    { 3, 1.5m, "CarHire" }
                });

            migrationBuilder.InsertData(
                table: "TravelProviders",
                columns: new[] { "TravelProviderId", "IsActive", "Name", "Type" },
                values: new object[,]
                {
                    { 1, true, "SkyBridge Air", "Flight" },
                    { 2, true, "Coastal Stays", "Hotel" },
                    { 3, true, "RoadReady Car Hire", "CarHire" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_AccountNumber",
                table: "Accounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_MemberId",
                table: "Accounts",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_AccountId_CreatedAt",
                table: "AccountTransactions",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_PurchaseId",
                table: "AccountTransactions",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_RedemptionId",
                table: "AccountTransactions",
                column: "RedemptionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_RefundId",
                table: "AccountTransactions",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_TravelBookingId",
                table: "AccountTransactions",
                column: "TravelBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductId",
                table: "CartItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_MemberId",
                table: "Carts",
                column: "MemberId",
                unique: true,
                filter: "[Status] = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_Deals_MinTierId",
                table: "Deals",
                column: "MinTierId");

            migrationBuilder.CreateIndex(
                name: "IX_DrivingMonthlySummaries_MemberId_Year_Month",
                table: "DrivingMonthlySummaries",
                columns: new[] { "MemberId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrivingRewardBands_MinScore",
                table: "DrivingRewardBands",
                column: "MinScore",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrivingTrips_ExternalTripId",
                table: "DrivingTrips",
                column: "ExternalTripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrivingTrips_MemberId_StartedAt",
                table: "DrivingTrips",
                columns: new[] { "MemberId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DrivingTrips_VehicleId",
                table: "DrivingTrips",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_Email",
                table: "Members",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_TierId",
                table: "Members",
                column: "TierId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AccountId",
                table: "Orders",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CartId",
                table: "Orders",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MemberId_PlacedAt",
                table: "Orders",
                columns: new[] { "MemberId", "PlacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PurchaseId",
                table: "Orders",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PointsLots_MemberId_ExpiresAt",
                table: "PointsLots",
                columns: new[] { "MemberId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_MemberId_Category_CreatedAt",
                table: "PointsTransactions",
                columns: new[] { "MemberId", "Category", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_MemberId_CreatedAt",
                table: "PointsTransactions",
                columns: new[] { "MemberId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category_IsActive",
                table: "Products",
                columns: new[] { "Category", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseItems_PurchaseId",
                table: "PurchaseItems",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_AccountId",
                table: "Purchases",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_ExternalReference",
                table: "Purchases",
                column: "ExternalReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_MemberId_PurchasedAt",
                table: "Purchases",
                columns: new[] { "MemberId", "PurchasedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_StoreId",
                table: "Purchases",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_AccountId",
                table: "Redemptions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_DealId",
                table: "Redemptions",
                column: "DealId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_MemberId",
                table: "Redemptions",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Redemptions_VoucherCode",
                table: "Redemptions",
                column: "VoucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PurchaseId",
                table: "Refunds",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TierHistories_FromTierId",
                table: "TierHistories",
                column: "FromTierId");

            migrationBuilder.CreateIndex(
                name: "IX_TierHistories_MemberId",
                table: "TierHistories",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TierHistories_ToTierId",
                table: "TierHistories",
                column: "ToTierId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_Rank",
                table: "Tiers",
                column: "Rank",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_AccountId",
                table: "TravelBookings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_BookingReference",
                table: "TravelBookings",
                column: "BookingReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_ListingId_StartDate_EndDate",
                table: "TravelBookings",
                columns: new[] { "ListingId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_MemberId",
                table: "TravelBookings",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_Status_EndDate",
                table: "TravelBookings",
                columns: new[] { "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelEarnRules_Type",
                table: "TravelEarnRules",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TravelListings_ProviderId",
                table: "TravelListings",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelListings_Type_Destination_AvailableFrom",
                table: "TravelListings",
                columns: new[] { "Type", "Destination", "AvailableFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelTravellers_TravelBookingId",
                table: "TravelTravellers",
                column: "TravelBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_MemberId",
                table: "Vehicles",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RegistrationNumber",
                table: "Vehicles",
                column: "RegistrationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_TelematicsDeviceId",
                table: "Vehicles",
                column: "TelematicsDeviceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTransactions");

            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropTable(
                name: "DrivingMonthlySummaries");

            migrationBuilder.DropTable(
                name: "DrivingRewardBands");

            migrationBuilder.DropTable(
                name: "DrivingTrips");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PointsLots");

            migrationBuilder.DropTable(
                name: "PointsTransactions");

            migrationBuilder.DropTable(
                name: "PurchaseItems");

            migrationBuilder.DropTable(
                name: "SpendMilestones");

            migrationBuilder.DropTable(
                name: "TierHistories");

            migrationBuilder.DropTable(
                name: "TravelEarnRules");

            migrationBuilder.DropTable(
                name: "TravelTravellers");

            migrationBuilder.DropTable(
                name: "Redemptions");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "TravelBookings");

            migrationBuilder.DropTable(
                name: "Deals");

            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropTable(
                name: "Purchases");

            migrationBuilder.DropTable(
                name: "TravelListings");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropTable(
                name: "TravelProviders");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Tiers");
        }
    }
}
