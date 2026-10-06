namespace Orders.Application;

public static class OrderInputLimits
{
    public const int MaximumItems = 100;
    public const int MaximumSkuLength = 64;
    public const int MaximumQuantityPerItem = 1_000;
    public const decimal MaximumUnitPrice = 1_000_000m;
    public const int MaximumCustomerIdLength = 128;
}
