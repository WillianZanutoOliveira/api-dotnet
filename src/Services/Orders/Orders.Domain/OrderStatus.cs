namespace Orders.Domain;

public enum OrderStatus
{
    Pending = 1,
    InventoryRejected = 2,
    PaymentFailed = 3,
    Completed = 4
}
