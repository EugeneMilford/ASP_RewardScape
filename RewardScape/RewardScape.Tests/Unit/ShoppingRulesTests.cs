using Shouldly;
using SpendWell.Api.Services;

namespace SpendWell.Tests.Unit;

public class ShoppingRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(19.99, 60)]
    [InlineData(499.99, 60)]
    [InlineData(500, 0)]
    [InlineData(1200, 0)]
    public void DeliveryFeeFor_Subtotal_ChargesR60UnderR500(decimal subtotal, decimal expected)
    {
        ShoppingRules.DeliveryFeeFor(subtotal).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(450, 50)]
    [InlineData(499.99, 0.01)]
    [InlineData(500, 0)]
    [InlineData(800, 0)]
    public void AmountToFreeDelivery_Subtotal_ShowsWhatsLeft(decimal subtotal, decimal expected)
    {
        ShoppingRules.AmountToFreeDelivery(subtotal).ShouldBe(expected);
    }
}
