namespace DistributedCommerce.Contracts;

public sealed record OrderLineMessage(string Sku, int Quantity, decimal UnitPrice);

public sealed record OrderSubmitted(
    Guid EventId,
    Guid OrderId,
    string CustomerId,
    IReadOnlyCollection<OrderLineMessage> Items,
    decimal TotalAmount,
    DateTimeOffset OccurredAt);

public sealed record InventoryReserved(
    Guid EventId,
    Guid OrderId,
    decimal TotalAmount,
    DateTimeOffset OccurredAt);

public sealed record InventoryRejected(
    Guid EventId,
    Guid OrderId,
    string Reason,
    DateTimeOffset OccurredAt);

public sealed record PaymentAuthorized(
    Guid EventId,
    Guid OrderId,
    string PaymentId,
    decimal Amount,
    DateTimeOffset OccurredAt);

public sealed record PaymentFailed(
    Guid EventId,
    Guid OrderId,
    string Reason,
    DateTimeOffset OccurredAt);
