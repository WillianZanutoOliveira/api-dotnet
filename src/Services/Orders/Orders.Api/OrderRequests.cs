namespace Orders.Api;

public sealed record CreateOrderRequest(IReadOnlyCollection<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(string Sku, int Quantity, decimal UnitPrice);
