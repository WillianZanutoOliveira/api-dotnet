namespace Inventory.Service;

public sealed class InventoryDecision
{
    private InventoryDecision() { }

    public InventoryDecision(Guid orderId, string status, string? reason)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Status = status;
        Reason = reason;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
