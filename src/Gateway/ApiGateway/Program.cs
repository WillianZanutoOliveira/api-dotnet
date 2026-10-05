using System.Threading.RateLimiting;
using DistributedCommerce.Security;
using DistributedCommerce.ServiceDefaults;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformIdentity(builder.Configuration);
builder.AddPlatformServiceDefaults("api-gateway");
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("authenticated-api", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey:
                context.User.FindFirst("sub")?.Value ??
                context.Connection.RemoteIpAddress?.ToString() ??
                "unknown",
            factory: static _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 60,
                TokensPerPeriod = 60,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapPlatformDefaultEndpoints();
app.MapReverseProxy()
    .RequireAuthorization()
    .RequireRateLimiting("authenticated-api");

await app.RunAsync();
