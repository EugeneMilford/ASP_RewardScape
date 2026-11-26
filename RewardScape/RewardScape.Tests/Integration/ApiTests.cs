using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using SpendWell.Api.DTOs;
using SpendWell.Api.Models;

namespace SpendWell.Tests.Integration;

/// <summary>
/// End-to-end: real HTTP requests through routing, validation, controllers, services,
/// the exception handler and real SQL Server. Needs Docker running.
/// Skip them with: dotnet test --filter "Category!=Integration"
/// </summary>
[Collection("Api")]
[Trait("Category", "Integration")]
public class ApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<MemberDto> SignUpAsync(decimal openingDeposit)
    {
        var dto = new CreateMemberDto(
            "Integration Test",
            string.Format("it-{0}@example.com", Guid.NewGuid().ToString("N")),
            "0821234567",
            openingDeposit);

        var response = await _client.PostAsJsonAsync("/api/members", dto, ApiFactory.Json);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var member = await response.Content.ReadFromJsonAsync<MemberDto>(ApiFactory.Json);
        return member!;
    }

    [Fact]
    public async Task SignUp_Returns201_WithAnAccount()
    {
        var member = await SignUpAsync(1000m);

        member.Tier.ShouldBe("Blue");
        member.Accounts.Single().Balance.ShouldBe(1000m);
    }

    [Fact]
    public async Task SignUp_BadEmail_Returns400()
    {
        var dto = new CreateMemberDto("Bad Email", "not-an-email", "0821234567", 0m);

        var response = await _client.PostAsJsonAsync("/api/members", dto, ApiFactory.Json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownMember_Returns404_WithProblemDetails()
    {
        var response = await _client.GetAsync("/api/members/987654");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problem!.Title.ShouldBe("Not found");
    }

    [Fact]
    public async Task TillPurchase_SameSlipTwice_Returns409()
    {
        var member = await SignUpAsync(5000m);
        var purchase = new CreatePurchaseDto(
            string.Format("IT-{0}", Guid.NewGuid().ToString("N")),
            member.Id,
            member.Accounts.Single().AccountNumber,
            1,
            new List<CreatePurchaseItemDto> { new CreatePurchaseItemDto("Groceries", ProductCategory.Groceries, 1, 100m) });

        var first = await _client.PostAsJsonAsync("/api/purchases", purchase, ApiFactory.Json);
        var second = await _client.PostAsJsonAsync("/api/purchases", purchase, ApiFactory.Json);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OnlineShopping_Journey_EndsUpOnTheSpendingSummary()
    {
        var member = await SignUpAsync(5000m);
        string accountNumber = member.Accounts.Single().AccountNumber;

        // Find the seeded earbuds (R599: over R500, so free delivery and 100 points)
        var search = await _client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?search=earbuds", ApiFactory.Json);
        var earbuds = search!.Items.First();

        var add = await _client.PostAsJsonAsync(
            string.Format("/api/members/{0}/cart/items", member.Id), new AddCartItemDto(earbuds.Id, 1), ApiFactory.Json);
        add.StatusCode.ShouldBe(HttpStatusCode.OK);

        var checkout = await _client.PostAsJsonAsync(
            string.Format("/api/members/{0}/cart/checkout", member.Id),
            new CheckoutDto(accountNumber, new AddressDto("12 Main Road", null, "Vredenburg", "Vredenburg", "7380")),
            ApiFactory.Json);
        checkout.StatusCode.ShouldBe(HttpStatusCode.Created);

        var result = await checkout.Content.ReadFromJsonAsync<CheckoutResultDto>(ApiFactory.Json);
        result!.PointsEarned.ShouldBe(100);
        result.AccountBalanceAfter.ShouldBe(5000m - earbuds.Price);

        var summary = await _client.GetFromJsonAsync<SpendingSummaryDto>(
            string.Format("/api/accounts/{0}/spending-summary", accountNumber), ApiFactory.Json);
        summary!.Purchases.Single().StoreName.ShouldBe("SpendWell Online");
        summary.TotalSpentOnPurchases.ShouldBe(earbuds.Price);

        var rewards = await _client.GetFromJsonAsync<RewardsSummaryDto>(
            string.Format("/api/members/{0}/rewards-summary", member.Id), ApiFactory.Json);
        rewards!.ByCategory.Single(c => c.Category == "Shopping").PointsThisMonth.ShouldBe(100);
    }

    [Fact]
    public async Task TwoCheckoutsForTheLastItem_OnlyOneSucceeds()
    {
        // A product with one unit left
        var create = await _client.PostAsJsonAsync("/api/admin/products",
            new CreateProductDto(string.Format("LAST-{0}", Guid.NewGuid().ToString("N").Substring(0, 8)),
                "Last one", "Only one left", ProductCategory.Electronics, 100m, 1, null),
            ApiFactory.Json);
        var product = await create.Content.ReadFromJsonAsync<ProductDto>(ApiFactory.Json);

        var buyerA = await SignUpAsync(1000m);
        var buyerB = await SignUpAsync(1000m);
        foreach (var buyer in new[] { buyerA, buyerB })
        {
            await _client.PostAsJsonAsync(
                string.Format("/api/members/{0}/cart/items", buyer.Id), new AddCartItemDto(product!.Id, 1), ApiFactory.Json);
        }

        var address = new AddressDto("1 Test Street", null, "Saldanha", "Saldanha", "7395");
        var checkouts = await Task.WhenAll(
            _client.PostAsJsonAsync(string.Format("/api/members/{0}/cart/checkout", buyerA.Id),
                new CheckoutDto(buyerA.Accounts.Single().AccountNumber, address), ApiFactory.Json),
            _client.PostAsJsonAsync(string.Format("/api/members/{0}/cart/checkout", buyerB.Id),
                new CheckoutDto(buyerB.Accounts.Single().AccountNumber, address), ApiFactory.Json));

        // One wins. The other loses on the stock check (400) or the RowVersion clash (409).
        checkouts.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);

        var after = await _client.GetFromJsonAsync<ProductDto>(string.Format("/api/products/{0}", product!.Id), ApiFactory.Json);
        after!.StockQuantity.ShouldBe(0);
    }
}
