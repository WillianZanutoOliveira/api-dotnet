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

var brasilApiBaseUrl =
    builder.Configuration["BrasilApi:BaseUrl"] ??
    "https://brasilapi.com.br/";

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

var customers = app.MapGroup("/customers");

customers.MapGet(
    "/address/cep/{postalCode}",
    async (string postalCode, CustomerService service, CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.LookupPostalCodeAsync(postalCode, cancellationToken);
            return Results.Ok(result);
        }
        catch (PostalCodeNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception);
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Postal code provider unavailable.");
        }
    })
    .RequireAuthorization(SecurityPolicies.CustomersRead);

customers.MapPost(
    "",
    async (CreateCustomerRequest request, CustomerService service, CancellationToken cancellationToken) =>
    {
        try
        {
            var created = await service.CreateAsync(Map(request), cancellationToken);
            return Results.Created($"/customers/{created.Id}", created);
        }
        catch (DuplicateCustomerDocumentException)
        {
            return Results.Conflict(new { message = "A customer with this document already exists." });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(exception);
        }
    })
    .RequireAuthorization(SecurityPolicies.CustomersWrite);

customers.MapGet(
    "/{id:guid}",
    async (Guid id, CustomerService service, CancellationToken cancellationToken) =>
    {
        var customer = await service.GetAsync(id, cancellationToken);
        return customer is null ? Results.NotFound() : Results.Ok(customer);
    })
    .RequireAuthorization(SecurityPolicies.CustomersRead);

customers.MapGet(
    "",
    async (
        string? search,
        string? document,
        string? personType,
        int? page,
        int? pageSize,
        CustomerService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await service.SearchAsync(
                new CustomerQuery(
                    search,
                    document,
                    personType,
                    page ?? 1,
                    pageSize ?? 20),
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(exception);
        }
    })
    .RequireAuthorization(SecurityPolicies.CustomersRead);

customers.MapPut(
    "/{id:guid}",
    async (
        Guid id,
        UpdateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.UpdateAsync(id, Map(request), cancellationToken));
        }
        catch (CustomerNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DuplicateCustomerDocumentException)
        {
            return Results.Conflict(new { message = "A customer with this document already exists." });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return ValidationProblem(exception);
        }
    })
    .RequireAuthorization(SecurityPolicies.CustomersWrite);

customers.MapDelete(
    "/{id:guid}",
    async (Guid id, CustomerService service, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound())
    .RequireAuthorization(SecurityPolicies.CustomersWrite);

await app.RunAsync();

static CreateCustomerCommand Map(CreateCustomerRequest request) =>
    new(
        request.PersonType,
        request.Document,
        request.DisplayName,
        request.LegalName,
        request.BirthDate,
        request.FoundationDate,
        request.StateRegistration,
        request.MunicipalRegistration,
        request.Email,
        request.Phone,
        MapAddresses(request.Addresses));

static UpdateCustomerCommand Map(UpdateCustomerRequest request) =>
    new(
        request.PersonType,
        request.Document,
        request.DisplayName,
        request.LegalName,
        request.BirthDate,
        request.FoundationDate,
        request.StateRegistration,
        request.MunicipalRegistration,
        request.Email,
        request.Phone,
        request.IsActive,
        MapAddresses(request.Addresses));

static IReadOnlyCollection<AddressInput> MapAddresses(
    IReadOnlyCollection<AddressRequest?>? addresses)
{
    if (addresses is null)
        return [];

    return addresses
        .Select(address =>
            address is null
                ? throw new ArgumentException("Address entries cannot be null.", nameof(addresses))
                : Map(address))
        .ToArray();
}

static AddressInput Map(AddressRequest request) =>
    new(
        request.Type,
        request.IsPrimary,
        request.PostalCode,
        request.Street,
        request.Number,
        request.Complement,
        request.Neighborhood,
        request.City,
        request.State,
        request.IbgeCityCode,
        request.Latitude,
        request.Longitude);

static IResult ValidationProblem(Exception exception) =>
    Results.ValidationProblem(
        new Dictionary<string, string[]>
        {
            ["request"] = [exception.Message]
        });
