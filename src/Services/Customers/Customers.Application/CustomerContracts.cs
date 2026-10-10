namespace Customers.Application;

public sealed record AddressInput(
    string Type,
    bool IsPrimary,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string? Neighborhood,
    string City,
    string State,
    string? IbgeCityCode,
    decimal? Latitude,
    decimal? Longitude);

public sealed record CreateCustomerCommand(
    string PersonType,
    string Document,
    string DisplayName,
    string? LegalName,
    DateOnly? BirthDate,
    DateOnly? FoundationDate,
    string? StateRegistration,
    string? MunicipalRegistration,
    string Email,
    string Phone,
    IReadOnlyCollection<AddressInput> Addresses);

public sealed record UpdateCustomerCommand(
    string PersonType,
    string Document,
    string DisplayName,
    string? LegalName,
    DateOnly? BirthDate,
    DateOnly? FoundationDate,
    string? StateRegistration,
    string? MunicipalRegistration,
    string Email,
    string Phone,
    bool IsActive,
    IReadOnlyCollection<AddressInput> Addresses);

public sealed record CustomerQuery(
    string? Search,
    string? Document,
    string? PersonType,
    int Page,
    int PageSize);

public sealed record AddressView(
    Guid Id,
    string Type,
    bool IsPrimary,
    string PostalCode,
    string Street,
    string Number,
    string? Complement,
    string? Neighborhood,
    string City,
    string State,
    string Country,
    string? IbgeCityCode,
    decimal? Latitude,
    decimal? Longitude);

public sealed record CustomerView(
    Guid Id,
    string PersonType,
    string Document,
    string DisplayName,
    string? LegalName,
    DateOnly? BirthDate,
    DateOnly? FoundationDate,
    string? StateRegistration,
    string? MunicipalRegistration,
    string Email,
    string Phone,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<AddressView> Addresses);

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record PostalCodeLookupResult(
    string PostalCode,
    string Street,
    string? Neighborhood,
    string City,
    string State,
    string? IbgeCityCode,
    decimal? Latitude,
    decimal? Longitude,
    string Provider);
