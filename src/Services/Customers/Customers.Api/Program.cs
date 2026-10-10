using System.Text.Json;
using System.Text.Json.Serialization;
using Customers.Api;
using Customers.Application;
using Customers.Infrastructure;
using DistributedCommerce.Secrets;
using DistributedCommerce.Security;
using DistributedCommerce.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
await builder.Configuration.AddVaultSecretsAsync();
builder.Services.AddVaultLeaseRenewal();

var connectionString = builder.Configuration.GetConnectionString("customers-db")
    ?? throw new InvalidOperationException(
        "Customers database connection must be supplied through the configured secret boundary.");

builder.Services.AddCustomersInfrastructure(connectionString);
builder.Services.AddScoped<CustomerService>();
builder.AddPlatformServiceDefaults("customers-api");
builder.AddPlatformWebSecurity();
builder.Services.AddPlatformIdentity(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.MaxDepth = 16;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var brasilApiBaseUrl = builder.Configuration["BrasilApi:BaseUrl"]
    ?? throw new InvalidOperationException("BrasilApi:BaseUrl is required.");

if (!Uri.TryCreate(brasilApiBaseUrl, UriKind.Absolute, out var brasilApiUri) ||
    brasilApiUri.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException("BrasilAPI base URL must be an absolute HTTPS URL.");
}

builder.Services.AddHttpClient<IPostalCodeLookup, BrasilApiPostalCodeLookup>(client =>
{
    client.BaseAddress = brasilApiUri;
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.UseExceptionHandler(new Microsoft.AspNetCore.Builder.ExceptionHandlerOptions
{
    StatusCodeSelector = exception =>
        exception is BadHttpRequestException badRequest
            ? badRequest.StatusCode
            : StatusCodes.Status500InternalServerError
});
app.UsePlatformWebSecurity();
app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapCustomerEndpoints();

await app.RunAsync();
