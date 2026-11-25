using SpendWell.Api.Data;

namespace SpendWell.Api.Services;

/// <summary>Online shop rules in one place so the cart and checkout always agree.</summary>
public static class ShoppingRules
{
    public const int OnlineStoreId = SeedData.OnlineStoreId;
    public const decimal FreeDeliveryThreshold = 500m;
    public const decimal StandardDeliveryFee = 60m;
    public const int MaxQuantityPerLine = 20;

    public static decimal DeliveryFeeFor(decimal subtotal)
    {
        if (subtotal <= 0)
        {
            return 0m;
        }

        if (subtotal >= FreeDeliveryThreshold)
        {
            return 0m;
        }

        return StandardDeliveryFee;
    }

    public static decimal AmountToFreeDelivery(decimal subtotal)
    {
        if (subtotal <= 0 || subtotal >= FreeDeliveryThreshold)
        {
            return 0m;
        }

        return FreeDeliveryThreshold - subtotal;
    }
}
