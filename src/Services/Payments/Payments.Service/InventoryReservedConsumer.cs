using DistributedCommerce.Contracts;
using DistributedCommerce.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Payments.Service;

public sealed class InventoryReservedConsumer(PaymentsDbContext dbContext)
    : IConsumer<InventoryReserved>
{
    public async Task Consume(ConsumeContext<InventoryReserved> context)
    {
        using var activity = PlatformTelemetry.ActivitySource.StartActivity("payments.authorize");
        activity?.SetTag("order.id", context.Message.OrderId);
        activity?.SetTag("payment.amount", context.Message.TotalAmount);

        var existing = await dbContext.Payments
            .SingleOrDefaultAsync(
                payment => payment.OrderId == context.Message.OrderId,
                context.CancellationToken);

        if (existing is not null)
            return;

        // Deterministic failure rule keeps the demo reproducible.
        var approved = context.Message.TotalAmount <= 5_000m;

        if (approved)
        {
            var paymentId = $"pay_{Guid.NewGuid():N}";

            dbContext.Payments.Add(
                new Payment(context.Message.OrderId, context.Message.TotalAmount, "Authorized", null));

            await context.Publish(
                new PaymentAuthorized(
                    Guid.NewGuid(),
                    context.Message.OrderId,
                    paymentId,
                    context.Message.TotalAmount,
                    DateTimeOffset.UtcNow),
                context.CancellationToken);
        }
        else
        {
            const string reason = "Demo payment policy rejected orders above 5,000.";

            dbContext.Payments.Add(
                new Payment(context.Message.OrderId, context.Message.TotalAmount, "Failed", reason));

            await context.Publish(
                new PaymentFailed(
                    Guid.NewGuid(),
                    context.Message.OrderId,
                    reason,
                    DateTimeOffset.UtcNow),
                context.CancellationToken);
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
        PlatformTelemetry.MessagesProcessed.Add(1, new KeyValuePair<string, object?>("message.type", nameof(InventoryReserved)));
    }
}
