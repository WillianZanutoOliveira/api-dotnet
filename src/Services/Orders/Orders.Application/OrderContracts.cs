namespace Orders.Application;

public sealed record CreateOrderItem(string Sku, int Quantity, decimal UnitPrice);

public sealed record CreateOrderCommand(
    string CustomerId,
    IReadOnlyCollection<CreateOrderItem> Items);

public sealed record OrderView(
    Guid Id,
    string CustomerId,
    decimal TotalAmount,
    string Status,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
