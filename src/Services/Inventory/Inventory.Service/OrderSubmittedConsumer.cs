using DistributedCommerce.Contracts;
using DistributedCommerce.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

public sealed class OrderSubmittedConsumer(InventoryDbContext dbContext)
    : IConsumer<OrderSubmitted>
{
    public async Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        using var activity = PlatformTelemetry.ActivitySource.StartActivity("inventory.reserve");
        activity?.SetTag("order.id", context.Message.OrderId);

        var alreadyProcessed = await dbContext.Decisions
            .AnyAsync(
                decision => decision.OrderId == context.Message.OrderId,
                context.CancellationToken);

        if (alreadyProcessed)
            return;

        var unavailableItem = context.Message.Items
            .FirstOrDefault(item => item.Quantity > 10);

        if (unavailableItem is not null)
        {
            var reason = $"SKU {unavailableItem.Sku} exceeds demo stock threshold.";

            dbContext.Decisions.Add(
                new InventoryDecision(context.Message.OrderId, "Rejected", reason));

            await context.Publish(
                new InventoryRejected(
                    Guid.NewGuid(),
                    context.Message.OrderId,
                    reason,
                    DateTimeOffset.UtcNow),
                context.CancellationToken);
        }
        else
        {
            dbContext.Decisions.Add(
                new InventoryDecision(context.Message.OrderId, "Reserved", null));

            await context.Publish(
                new InventoryReserved(
                    Guid.NewGuid(),
                    context.Message.OrderId,
                    context.Message.TotalAmount,
                    DateTimeOffset.UtcNow),
                context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        PlatformTelemetry.MessagesProcessed.Add(1, new KeyValuePair<string, object?>("message.type", nameof(OrderSubmitted)));
    }
}
