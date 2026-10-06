using System.Threading.RateLimiting;
using DistributedCommerce.Security;
using DistributedCommerce.ServiceDefaults;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformIdentity(builder.Configuration, builder.Environment);
builder.AddPlatformServiceDefaults("api-gateway");
builder.AddPlatformWebSecurity();
builder.Services.AddProblemDetails();
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var rateLimitTokenLimit = builder.Configuration.GetValue("RateLimiting:TokenLimit", 60);
var rateLimitTokensPerPeriod = builder.Configuration.GetValue("RateLimiting:TokensPerPeriod", 60);
var rateLimitPeriodSeconds = builder.Configuration.GetValue("RateLimiting:PeriodSeconds", 60);

if (rateLimitTokenLimit <= 0 ||
    rateLimitTokensPerPeriod <= 0 ||
    rateLimitPeriodSeconds <= 0)
{
    throw new InvalidOperationException("Rate limiting configuration must use positive values.");
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static (context, _) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        return ValueTask.CompletedTask;
    };

    options.AddPolicy("authenticated-api", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey:
                context.User.FindFirst("sub")?.Value ??
                context.Connection.RemoteIpAddress?.ToString() ??
                "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = rateLimitTokenLimit,
                TokensPerPeriod = rateLimitTokensPerPeriod,
                ReplenishmentPeriod = TimeSpan.FromSeconds(rateLimitPeriodSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UsePlatformWebSecurity();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapPlatformDefaultEndpoints();
app.MapReverseProxy()
    .RequireAuthorization()
    .RequireRateLimiting("authenticated-api");

await app.RunAsync();
