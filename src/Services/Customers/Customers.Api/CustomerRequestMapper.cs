using Customers.Application;

namespace Customers.Api;

internal static class CustomerRequestMapper
{
    public static CreateCustomerCommand ToCommand(CreateCustomerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateCustomerCommand(
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
    }

    public static UpdateCustomerCommand ToCommand(UpdateCustomerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateCustomerCommand(
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
    }

    private static IReadOnlyCollection<AddressInput> MapAddresses(
        IReadOnlyCollection<AddressRequest?>? addresses)
    {
        if (addresses is null)
            return [];

        var mapped = new List<AddressInput>(addresses.Count);

        foreach (var address in addresses)
        {
            if (address is null)
                throw new ArgumentException("Address entries cannot be null.", nameof(addresses));

            mapped.Add(
                new AddressInput(
                    address.Type,
                    address.IsPrimary,
                    address.PostalCode,
                    address.Street,
                    address.Number,
                    address.Complement,
                    address.Neighborhood,
                    address.City,
                    address.State,
                    address.IbgeCityCode,
                    address.Latitude,
                    address.Longitude));
        }

        return mapped;
    }
}
