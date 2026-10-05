using System.Security.Claims;
using DistributedCommerce.Observability;
using DistributedCommerce.Security;
using MassTransit;
using Orders.Application;
using Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var ordersConnection = builder.Configuration.GetConnectionString("orders-db")
    ?? "Host=localhost;Port=5432;Database=orders;Username=postgres;Password=postgres";

var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMq:Username"] ?? "guest";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "guest";

builder.Services.AddOrdersInfrastructure(ordersConnection);
builder.Services.AddScoped<OrderService>();
builder.Services.AddPlatformObservability(builder.Configuration, "orders-api");
builder.Services.AddPlatformIdentity(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

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

app.Run();

static Dictionary<string, string[]> Validate(CreateOrderRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (request.Items is null || request.Items.Count == 0)
        errors["items"] = ["At least one item is required."];
    else if (request.Items.Any(item =>
                 string.IsNullOrWhiteSpace(item.Sku) ||
                 item.Quantity <= 0 ||
                 item.UnitPrice <= 0))
        errors["items"] = ["Every item requires a SKU, positive quantity and positive unit price."];

    return errors;
}

public sealed record CreateOrderRequest(IReadOnlyCollection<CreateOrderItemRequest> Items);
public sealed record CreateOrderItemRequest(string Sku, int Quantity, decimal UnitPrice);
