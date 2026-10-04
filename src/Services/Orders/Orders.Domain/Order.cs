namespace Orders.Domain;

public sealed class Order
{
    private Order() { }

    private Order(Guid id, string customerId, decimal totalAmount)
    {
        Id = id;
        CustomerId = customerId;
        TotalAmount = totalAmount;
        Status = OrderStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Order Create(string customerId, decimal totalAmount)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer id is required.", nameof(customerId));

        if (totalAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalAmount), "Order total must be greater than zero.");

        return new Order(Guid.NewGuid(), customerId.Trim(), totalAmount);
    }

    public void MarkCompleted()
    {
        EnsurePending();
        Status = OrderStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RejectInventory(string reason)
    {
        EnsurePending();
        Status = OrderStatus.InventoryRejected;
        FailureReason = NormalizeReason(reason);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPaymentFailed(string reason)
    {
        EnsurePending();
        Status = OrderStatus.PaymentFailed;
        FailureReason = NormalizeReason(reason);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Order {Id} is already in terminal state {Status}.");
    }

    private static string NormalizeReason(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? "Unspecified failure." : reason.Trim();
}
