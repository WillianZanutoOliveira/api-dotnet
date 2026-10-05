using DistributedCommerce.Secrets;
using DistributedCommerce.ServiceDefaults;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        await builder.Configuration.AddVaultSecretsAsync();
        builder.Services.AddVaultLeaseRenewal();

        var connectionString = builder.Configuration.GetConnectionString("inventory-db")
            ?? "Host=localhost;Port=5433;Database=inventory;Username=postgres;Password=postgres";

        var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "guest";
        var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

        builder.Services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.AddPlatformServiceDefaults("inventory-service");

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumer<OrderSubmittedConsumer>();

            x.AddEntityFrameworkOutbox<InventoryDbContext>(outbox =>
            {
                outbox.UsePostgres();
            });

            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry => retry.Intervals(200, 1_000, 5_000));
                endpoint.UseEntityFrameworkOutbox<InventoryDbContext>(context);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitHost, "/", host =>
                {
                    host.Username(rabbitUser);
                    host.Password(rabbitPassword);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        var app = builder.Build();

        app.MapPlatformDefaultEndpoints();
        await app.RunAsync();
    }
}
