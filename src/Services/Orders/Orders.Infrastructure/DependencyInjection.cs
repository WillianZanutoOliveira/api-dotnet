using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Application;

namespace Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrdersInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrdersDbContext>());
        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        return services;
    }
}
