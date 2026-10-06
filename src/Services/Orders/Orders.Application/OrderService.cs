using DistributedCommerce.Contracts;
using Orders.Domain;

namespace Orders.Application;

public sealed class OrderService(
    IOrderRepository repository,
    IIntegrationEventPublisher publisher,
    IUnitOfWork unitOfWork)
{
    public async Task<OrderView> CreateAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.CustomerId) ||
            command.CustomerId.Length > OrderInputLimits.MaximumCustomerIdLength)
        {
            throw new ArgumentException("CustomerId is invalid.");
        }

        if (command.Items is null ||
            command.Items.Count == 0 ||
            command.Items.Count > OrderInputLimits.MaximumItems)
        {
            throw new ArgumentException(
                $"Orders must contain between 1 and {OrderInputLimits.MaximumItems} items.");
        }

        if (command.Items.Any(item =>
                string.IsNullOrWhiteSpace(item.Sku) ||
                item.Sku.Trim().Length > OrderInputLimits.MaximumSkuLength ||
                item.Quantity <= 0 ||
                item.Quantity > OrderInputLimits.MaximumQuantityPerItem ||
                item.UnitPrice <= 0 ||
                item.UnitPrice > OrderInputLimits.MaximumUnitPrice))
        {
            throw new ArgumentException("One or more order items violate the accepted limits.");
        }

        var total = command.Items.Sum(item => item.Quantity * item.UnitPrice);
        var order = Order.Create(command.CustomerId, total);

        await repository.AddAsync(order, cancellationToken);

        await publisher.PublishAsync(
            new OrderSubmitted(
                EventId: Guid.NewGuid(),
                OrderId: order.Id,
                CustomerId: order.CustomerId,
                Items: command.Items
                    .Select(item => new OrderLineMessage(item.Sku.Trim(), item.Quantity, item.UnitPrice))
                    .ToArray(),
                TotalAmount: order.TotalAmount,
                OccurredAt: DateTimeOffset.UtcNow),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(order);
    }

    public async Task<OrderView?> GetAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(orderId, cancellationToken);
        return order is null ? null : Map(order);
    }

    private static OrderView Map(Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Status.ToString(),
            order.FailureReason,
            order.CreatedAt,
            order.UpdatedAt);
}
