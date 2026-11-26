using Shouldly;
using SpendWell.Api.Common;
using SpendWell.Api.Models;
using SpendWell.Api.Services;

namespace SpendWell.Tests.Unit;

public class DealPricingTests
{
    // CashPriceForTier is pure maths; no database needed
    private readonly DealService _deals = new DealService(null!, null!);

    [Theory]
    [InlineData(0, 112.50)]      // Blue
    [InlineData(5, 106.88)]      // Silver: 106.875 rounds up
    [InlineData(10, 101.25)]     // Gold
    [InlineData(20, 90.00)]      // Diamond
    public void CashPriceForTier_AppliesTheTierDiscount(decimal discountPercent, decimal expected)
    {
        var deal = new Deal { CashPrice = 112.50m };
        var tier = new Tier { DealDiscountPercent = discountPercent };

        _deals.CashPriceForTier(deal, tier).ShouldBe(expected);
    }

    [Fact]
    public void CashPriceForTier_PointsOnlyDeal_IsFree()
    {
        var deal = new Deal { CashPrice = 0m };
        var tier = new Tier { DealDiscountPercent = 20m };

        _deals.CashPriceForTier(deal, tier).ShouldBe(0m);
    }
}

public class CodeGeneratorTests
{
    [Fact]
    public void Random_ReturnsRequestedLength()
    {
        CodeGenerator.Random(6).Length.ShouldBe(6);
    }

    [Fact]
    public void Random_NeverUsesLookAlikeCharacters()
    {
        for (int i = 0; i < 200; i++)
        {
            string code = CodeGenerator.Random(12);
            code.IndexOfAny(new[] { '0', 'O', '1', 'I' }).ShouldBe(-1);
        }
    }
}
