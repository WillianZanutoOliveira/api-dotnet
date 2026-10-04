using DistributedCommerce.Contracts;
using DistributedCommerce.Observability;
using MassTransit;

namespace Notifications.Service;

public sealed class PaymentAuthorizedConsumer(
    ILogger<PaymentAuthorizedConsumer> logger) : IConsumer<PaymentAuthorized>
{
    public Task Consume(ConsumeContext<PaymentAuthorized> context)
    {
        using var activity = PlatformTelemetry.ActivitySource.StartActivity("notifications.payment-authorized");

        logger.LogInformation(
            "Order {OrderId} paid successfully with payment {PaymentId}. Notification queued for customer communication.",
            context.Message.OrderId,
            context.Message.PaymentId);

        PlatformTelemetry.MessagesProcessed.Add(1, new KeyValuePair<string, object?>("message.type", nameof(PaymentAuthorized)));
        return Task.CompletedTask;
    }
}

public sealed class PaymentFailedConsumer(
    ILogger<PaymentFailedConsumer> logger) : IConsumer<PaymentFailed>
{
    public Task Consume(ConsumeContext<PaymentFailed> context)
    {
        using var activity = PlatformTelemetry.ActivitySource.StartActivity("notifications.payment-failed");

        logger.LogWarning(
            "Payment failed for order {OrderId}: {Reason}. Customer failure notification queued.",
            context.Message.OrderId,
            context.Message.Reason);

        PlatformTelemetry.MessagesProcessed.Add(1, new KeyValuePair<string, object?>("message.type", nameof(PaymentFailed)));
        return Task.CompletedTask;
    }
}
