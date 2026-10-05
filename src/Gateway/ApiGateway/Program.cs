using DistributedCommerce.Observability;
using DistributedCommerce.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformIdentity(builder.Configuration);
builder.Services.AddPlatformObservability(builder.Configuration, "api-gateway");
builder.Services.AddHealthChecks();
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapReverseProxy().RequireAuthorization();

await app.RunAsync();
