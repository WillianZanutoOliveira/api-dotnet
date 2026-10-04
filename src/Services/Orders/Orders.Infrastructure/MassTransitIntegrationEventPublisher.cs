using MassTransit;
using Orders.Application;

namespace Orders.Infrastructure;

public sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint)
    : IIntegrationEventPublisher
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken)
        where T : class =>
        publishEndpoint.Publish(message, cancellationToken);
}
