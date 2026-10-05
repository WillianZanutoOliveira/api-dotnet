using DistributedCommerce.Observability;
using DistributedCommerce.Secrets;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
await builder.Configuration.AddVaultSecretsAsync();

        var connectionString = builder.Configuration.GetConnectionString("inventory-db")
            ?? "Host=localhost;Port=5433;Database=inventory;Username=postgres;Password=postgres";

        var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "guest";
        var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

        builder.Services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.Services.AddPlatformObservability(builder.Configuration, "inventory-service");
        builder.Services.AddHealthChecks();

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

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        app.MapHealthChecks("/health");
        await app.RunAsync();
    }
}
