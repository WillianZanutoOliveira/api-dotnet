using DistributedCommerce.ServiceDefaults;
using DistributedCommerce.Secrets;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Payments.Service;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        await builder.Configuration.AddVaultSecretsAsync();
        builder.Services.AddVaultLeaseRenewal();

        var connectionString = builder.Configuration.GetConnectionString("payments-db")
            ?? "Host=localhost;Port=5434;Database=payments;Username=postgres;Password=postgres";

        var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "guest";
        var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

        builder.Services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString));

        builder.AddPlatformServiceDefaults("payments-service");

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumer<InventoryReservedConsumer>();

            x.AddEntityFrameworkOutbox<PaymentsDbContext>(outbox =>
            {
                outbox.UsePostgres();
            });

            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry => retry.Intervals(200, 1_000, 5_000));
                endpoint.UseEntityFrameworkOutbox<PaymentsDbContext>(context);
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
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        app.MapPlatformDefaultEndpoints();
        await app.RunAsync();
    }
}
