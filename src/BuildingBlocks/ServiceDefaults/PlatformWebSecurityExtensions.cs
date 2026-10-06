using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DistributedCommerce.ServiceDefaults;

public static class PlatformWebSecurityExtensions
{
    private const long MaximumRequestBodySize = 1_048_576;

    public static WebApplicationBuilder AddPlatformWebSecurity(
        this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = MaximumRequestBodySize;
            options.Limits.MaxRequestLineSize = 4 * 1024;
            options.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
            options.Limits.MaxRequestHeaderCount = 50;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(120);
        });

        builder.Services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = false;
            options.Preload = false;
        });

        return builder;
    }

    public static WebApplication UsePlatformWebSecurity(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            app.UseHsts();

        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsTrace(context.Request.Method) ||
                string.Equals(context.Request.Method, "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.Headers.Allow = "GET, HEAD, POST, OPTIONS";
                return;
            }

            context.Response.OnStarting(
                static state =>
                {
                    var httpContext = (HttpContext)state;
                    var headers = httpContext.Response.Headers;

                    headers.TryAdd("X-Content-Type-Options", "nosniff");
                    headers.TryAdd("X-Frame-Options", "DENY");
                    headers.TryAdd("Referrer-Policy", "no-referrer");
                    headers.TryAdd(
                        "Permissions-Policy",
                        "camera=(), microphone=(), geolocation=(), payment=(), usb=()");
                    headers.TryAdd(
                        "Content-Security-Policy",
                        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'");
                    headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");

                    if (httpContext.Request.Headers.ContainsKey("Authorization"))
                        headers.TryAdd("Cache-Control", "no-store");

                    return Task.CompletedTask;
                },
                context);

            await next();
        });

        return app;
    }
}
