using DistributedCommerce.Observability;
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
app.MapHealthChecks("/health");

app.MapPost("/orders", async (
    CreateOrderRequest request,
    OrderService service,
    CancellationToken cancellationToken) =>
{
    var errors = Validate(request);

    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var order = await service.CreateAsync(
        new CreateOrderCommand(
            request.CustomerId,
            request.Items
                .Select(item => new CreateOrderItem(item.Sku, item.Quantity, item.UnitPrice))
                .ToArray()),
        cancellationToken);

    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    OrderService service,
    CancellationToken cancellationToken) =>
{
    var order = await service.GetAsync(id, cancellationToken);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();

static Dictionary<string, string[]> Validate(CreateOrderRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.CustomerId))
        errors["customerId"] = ["CustomerId is required."];

    if (request.Items is null || request.Items.Count == 0)
        errors["items"] = ["At least one item is required."];
    else if (request.Items.Any(item =>
                 string.IsNullOrWhiteSpace(item.Sku) ||
                 item.Quantity <= 0 ||
                 item.UnitPrice <= 0))
        errors["items"] = ["Every item requires a SKU, positive quantity and positive unit price."];

    return errors;
}

public sealed record CreateOrderRequest(string CustomerId, IReadOnlyCollection<CreateOrderItemRequest> Items);
public sealed record CreateOrderItemRequest(string Sku, int Quantity, decimal UnitPrice);
