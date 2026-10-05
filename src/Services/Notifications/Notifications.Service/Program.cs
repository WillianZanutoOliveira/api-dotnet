using DistributedCommerce.ServiceDefaults;
using DistributedCommerce.Secrets;
using MassTransit;

namespace Notifications.Service;

public static partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        await builder.Configuration.AddVaultSecretsAsync();
        builder.Services.AddVaultLeaseRenewal();

        var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "guest";
        var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

        builder.AddPlatformServiceDefaults("notifications-service");

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumer<PaymentAuthorizedConsumer>();
            x.AddConsumer<PaymentFailedConsumer>();

            x.AddConfigureEndpointsCallback((_, _, endpoint) =>
                endpoint.UseMessageRetry(retry => retry.Intervals(200, 1_000, 5_000)));

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
