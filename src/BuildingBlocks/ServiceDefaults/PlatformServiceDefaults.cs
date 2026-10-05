using DistributedCommerce.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace DistributedCommerce.ServiceDefaults;

public static class PlatformServiceDefaults
{
    public static TBuilder AddPlatformServiceDefaults<TBuilder>(
        this TBuilder builder,
        string serviceName)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        builder.Services.AddPlatformObservability(builder.Configuration, serviceName);

        builder.Services
            .AddHealthChecks()
            .AddCheck(
                "self",
                static () => HealthCheckResult.Healthy(),
                tags: ["live"]);

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static WebApplication MapPlatformDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks(
            "/alive",
            new HealthCheckOptions
            {
                Predicate = static registration => registration.Tags.Contains("live")
            });

        return app;
    }
}
