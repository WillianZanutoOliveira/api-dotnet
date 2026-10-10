using Customers.Application;
using DistributedCommerce.Security;

namespace Customers.Api;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var customers = endpoints.MapGroup("/customers");

        customers.MapGet("/address/cep/{postalCode}", LookupPostalCodeAsync)
            .RequireAuthorization(SecurityPolicies.CustomersRead);

        customers.MapPost("", CreateAsync)
            .RequireAuthorization(SecurityPolicies.CustomersWrite);

        customers.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(SecurityPolicies.CustomersRead);

        customers.MapGet("", SearchAsync)
            .RequireAuthorization(SecurityPolicies.CustomersRead);

        customers.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(SecurityPolicies.CustomersWrite);

        customers.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization(SecurityPolicies.CustomersWrite);

        return endpoints;
    }

    private static async Task<IResult> LookupPostalCodeAsync(
        string postalCode,
        CustomerService service,
        CancellationToken cancellationToken)
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
    }

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateAsync(
                CustomerRequestMapper.ToCommand(request),
                cancellationToken);

            return Results.Created($"/customers/{created.Id}", created);
        }
        catch (DuplicateCustomerDocumentException)
        {
            return DocumentConflict();
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return ValidationProblem(exception);
        }
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        var customer = await service.GetAsync(id, cancellationToken);
        return customer is null ? Results.NotFound() : Results.Ok(customer);
    }

    private static async Task<IResult> SearchAsync(
        string? search,
        string? document,
        string? personType,
        int? page,
        int? pageSize,
        CustomerService service,
        CancellationToken cancellationToken)
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
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CustomerService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.UpdateAsync(
                id,
                CustomerRequestMapper.ToCommand(request),
                cancellationToken);

            return Results.Ok(updated);
        }
        catch (CustomerNotFoundException)
        {
            return Results.NotFound();
        }
        catch (DuplicateCustomerDocumentException)
        {
            return DocumentConflict();
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return ValidationProblem(exception);
        }
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        CustomerService service,
        CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();

    private static bool IsValidationException(Exception exception) =>
        exception is ArgumentException or InvalidOperationException;

    private static IResult DocumentConflict() =>
        Results.Conflict(new { message = "A customer with this document already exists." });

    private static IResult ValidationProblem(Exception exception) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message]
            });
}
