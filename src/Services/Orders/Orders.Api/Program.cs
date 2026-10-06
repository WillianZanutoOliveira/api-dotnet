using System.Security.Claims;
using DistributedCommerce.Secrets;
using DistributedCommerce.Security;
using DistributedCommerce.ServiceDefaults;
using MassTransit;
using Orders.Api;
using Orders.Application;
using Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
await builder.Configuration.AddVaultSecretsAsync();
builder.Services.AddVaultLeaseRenewal();

var ordersConnection = builder.Configuration.GetConnectionString("orders-db")
    ?? throw new InvalidOperationException(
        "Orders database connection must be supplied through the configured secret boundary.");

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:Username"]
    ?? throw new InvalidOperationException("RabbitMQ username is required.");
var rabbitPassword = builder.Configuration["RabbitMq:Password"]
    ?? throw new InvalidOperationException("RabbitMQ password is required.");

builder.Services.AddOrdersInfrastructure(ordersConnection);
builder.Services.AddScoped<OrderService>();
builder.AddPlatformServiceDefaults("orders-api");
builder.AddPlatformWebSecurity();
builder.Services.AddPlatformIdentity(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.AddConsumer<InventoryRejectedConsumer>();
    x.AddConsumer<PaymentAuthorizedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();

    x.AddEntityFrameworkOutbox<OrdersDbContext>(outbox =>
    {
        outbox.UsePostgres();
        outbox.UseBusOutbox();
    });

    x.AddConfigureEndpointsCallback((context, _, endpoint) =>
    {
        endpoint.UseMessageRetry(retry => retry.Intervals(100, 500, 1_000));
        endpoint.UseEntityFrameworkOutbox<OrdersDbContext>(context);
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

app.UseExceptionHandler();
app.UsePlatformWebSecurity();
app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformDefaultEndpoints();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/me", (ClaimsPrincipal user) =>
{
    var roles = user.FindAll("roles").Select(claim => claim.Value).Distinct().Order().ToArray();

    return Results.Ok(new
    {
        subject = user.FindFirst("sub")?.Value,
        username = user.FindFirst("preferred_username")?.Value,
        roles
    });
}).RequireAuthorization();

app.MapPost("/orders", async (
    CreateOrderRequest request,
    ClaimsPrincipal user,
    OrderService service,
    CancellationToken cancellationToken) =>
{
    var customerId = user.FindFirst("sub")?.Value;
    if (string.IsNullOrWhiteSpace(customerId))
        return Results.Unauthorized();

    var errors = Validate(request);
    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var order = await service.CreateAsync(
        new CreateOrderCommand(
            customerId,
            request.Items
                .Select(item => new CreateOrderItem(item.Sku, item.Quantity, item.UnitPrice))
                .ToArray()),
        cancellationToken);

    return Results.Created($"/orders/{order.Id}", order);
}).RequireAuthorization(SecurityPolicies.OrdersWrite);

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    OrderService service,
    CancellationToken cancellationToken) =>
{
    var order = await service.GetAsync(id, cancellationToken);
    if (order is null)
        return Results.NotFound();

    var subject = user.FindFirst("sub")?.Value;
    var isAdmin = user.IsInRole("admin");

    if (!isAdmin && !string.Equals(order.CustomerId, subject, StringComparison.Ordinal))
        return Results.Forbid();

    return Results.Ok(order);
}).RequireAuthorization(SecurityPolicies.OrdersRead);

await app.RunAsync();

static Dictionary<string, string[]> Validate(CreateOrderRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (request.Items is null ||
        request.Items.Count == 0 ||
        request.Items.Count > OrderInputLimits.MaximumItems)
    {
        errors["items"] =
        [
            $"Orders must contain between 1 and {OrderInputLimits.MaximumItems} items."
        ];
    }
    else if (request.Items.Any(item =>
                 string.IsNullOrWhiteSpace(item.Sku) ||
                 item.Sku.Trim().Length > OrderInputLimits.MaximumSkuLength ||
                 item.Quantity <= 0 ||
                 item.Quantity > OrderInputLimits.MaximumQuantityPerItem ||
                 item.UnitPrice <= 0 ||
                 item.UnitPrice > OrderInputLimits.MaximumUnitPrice))
    {
        errors["items"] =
        [
            "Every item must use a bounded SKU, quantity and unit price."
        ];
    }

    return errors;
}
