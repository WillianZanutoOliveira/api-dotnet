using DistributedCommerce.Contracts;
using DistributedCommerce.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Orders.Domain;

namespace Orders.Infrastructure;

public sealed class InventoryRejectedConsumer(OrdersDbContext dbContext)
    : IConsumer<InventoryRejected>
{
    public async Task Consume(ConsumeContext<InventoryRejected> context)
    {
        using var activity = PlatformTelemetry.ActivitySource.StartActivity("orders.inventory-rejected");

        var order = await dbContext.Orders
            .SingleOrDefaultAsync(order => order.Id == context.Message.OrderId, context.CancellationToken);

        if (order is null || order.Status != OrderStatus.Pending)
            return;

        order.RejectInventory(context.Message.Reason);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        PlatformTelemetry.MessagesProcessed.Add(1, new KeyValuePair<string, object?>("message.type", nameof(InventoryRejected)));
    }
}

public sealed class PaymentAuthorizedConsumer(OrdersDbContext dbContext)
    : IConsumer<PaymentAuthorized>
{
    public async Task Consume(ConsumeContext<PaymentAuthorized> context)
    {
        using var activity = PlatformTelemetry.ActivitySource.StartActivity("orders.payment-authorized");

        var order = await dbContext.Orders
            .SingleOrDefaultAsync(order => order.Id == context.Message.OrderId, context.CancellationToken);

        if (order is null || order.Status != OrderStatus.Pending)
            return;

        order.MarkCompleted();
        await dbContext.SaveChangesAsync(context.CancellationToken);

        PlatformTelemetry.MessagesProcessed.Add(1, new KeyValuePair<string, object?>("message.type", nameof(PaymentAuthorized)));
    }
}

public sealed class PaymentFailedConsumer(OrdersDbContext dbContext)
    : IConsumer<PaymentFailed>
{
    public async Task Consume(ConsumeContext<PaymentFailed> context)
    {
        using var activity = PlatformTelemetry.ActivitySource.StartActivity("orders.payment-failed");

        var order = await dbContext.Orders
            .SingleOrDefaultAsync(order => order.Id == context.Message.OrderId, context.CancellationToken);

        if (order is null || order.Status != OrderStatus.Pending)
            return;

        order.MarkPaymentFailed(context.Message.Reason);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        PlatformTelemetry.MessagesProcessed.Add(1, new KeyValuePair<string, object?>("message.type", nameof(PaymentFailed)));
    }
}
