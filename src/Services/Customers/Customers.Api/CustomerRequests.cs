namespace Customers.Api;

public sealed record AddressRequest(
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

public sealed record CreateCustomerRequest(
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
    IReadOnlyCollection<AddressRequest?>? Addresses);

public sealed record UpdateCustomerRequest(
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
    IReadOnlyCollection<AddressRequest?>? Addresses);
