namespace Payments.Service;

public sealed class Payment
{
    private Payment() { }

    public Payment(Guid orderId, decimal amount, string status, string? reason)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Amount = amount;
        Status = status;
        Reason = reason;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
