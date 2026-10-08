using Customers.Domain;

namespace Customers.Application;

public sealed class CustomerService(
    ICustomerRepository repository,
    IUnitOfWork unitOfWork,
    IPostalCodeLookup postalCodeLookup)
{
    public async Task<CustomerView> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var personType = ParsePersonType(command.PersonType);
        var normalizedDocument = DocumentValidator.NormalizeAndValidate(personType, command.Document);

        if (await repository.DocumentExistsAsync(normalizedDocument, null, cancellationToken))
            throw new DuplicateCustomerDocumentException();

        var customer = Customer.Create(
            personType,
            normalizedDocument,
            command.DisplayName,
            command.LegalName,
            command.BirthDate,
            command.FoundationDate,
            command.StateRegistration,
            command.MunicipalRegistration,
            command.Email,
            command.Phone,
            MapAddresses(command.Addresses));

        await repository.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(customer);
    }

    public async Task<CustomerView?> GetAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(customerId, cancellationToken);
        return customer is null ? null : Map(customer);
    }

    public async Task<PagedResult<CustomerView>> SearchAsync(
        CustomerQuery query,
        CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        var normalizedQuery = query with
        {
            PersonType = string.IsNullOrWhiteSpace(query.PersonType)
                ? null
                : ParsePersonType(query.PersonType).ToString(),
            Document = NormalizeSearchDocument(query.Document),
            Page = page,
            PageSize = pageSize
        };

        var (items, totalCount) = await repository.SearchAsync(
            normalizedQuery,
            cancellationToken);

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<CustomerView>(
            items.Select(Map).ToArray(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<CustomerView> UpdateAsync(
        Guid customerId,
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var customer = await repository.GetForUpdateAsync(customerId, cancellationToken)
            ?? throw new CustomerNotFoundException(customerId);

        var personType = ParsePersonType(command.PersonType);
        var normalizedDocument = DocumentValidator.NormalizeAndValidate(personType, command.Document);

        if (await repository.DocumentExistsAsync(
                normalizedDocument,
                customerId,
                cancellationToken))
        {
            throw new DuplicateCustomerDocumentException();
        }

        customer.Update(
            personType,
            normalizedDocument,
            command.DisplayName,
            command.LegalName,
            command.BirthDate,
            command.FoundationDate,
            command.StateRegistration,
            command.MunicipalRegistration,
            command.Email,
            command.Phone,
            MapAddresses(command.Addresses),
            command.IsActive);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(customer);
    }

    public async Task<bool> DeleteAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var customer = await repository.GetForUpdateAsync(customerId, cancellationToken);

        if (customer is null)
            return false;

        repository.Remove(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<PostalCodeLookupResult> LookupPostalCodeAsync(
        string postalCode,
        CancellationToken cancellationToken) =>
        postalCodeLookup.LookupAsync(
            Address.NormalizePostalCode(postalCode),
            cancellationToken);

    private static PersonType ParsePersonType(string value) =>
        Enum.TryParse<PersonType>(value, ignoreCase: true, out var parsed) &&
        Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException("PersonType must be Individual or Company.", nameof(value));

    private static AddressType ParseAddressType(string value) =>
        Enum.TryParse<AddressType>(value, ignoreCase: true, out var parsed) &&
        Enum.IsDefined(parsed)
            ? parsed
            : throw new ArgumentException(
                "Address type must be Primary, Billing, Shipping or Other.",
                nameof(value));

    private static IReadOnlyCollection<AddressDetails> MapAddresses(
        IReadOnlyCollection<AddressInput> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        return addresses
            .Select(address => new AddressDetails(
                ParseAddressType(address.Type),
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
                address.Longitude))
            .ToArray();
    }

    private static string? NormalizeSearchDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
            return null;

        var normalized = new string(document.Where(char.IsAsciiDigit).ToArray());
        return normalized.Length == 0 ? null : normalized;
    }

    private static CustomerView Map(Customer customer) =>
        new(
            customer.Id,
            customer.PersonType.ToString(),
            customer.Document,
            customer.DisplayName,
            customer.LegalName,
            customer.BirthDate,
            customer.FoundationDate,
            customer.StateRegistration,
            customer.MunicipalRegistration,
            customer.Email,
            customer.Phone,
            customer.IsActive,
            customer.CreatedAt,
            customer.UpdatedAt,
            customer.Addresses
                .OrderByDescending(address => address.IsPrimary)
                .ThenBy(address => address.Type)
                .Select(address => new AddressView(
                    address.Id,
                    address.Type.ToString(),
                    address.IsPrimary,
                    address.PostalCode,
                    address.Street,
                    address.Number,
                    address.Complement,
                    address.Neighborhood,
                    address.City,
                    address.State,
                    address.Country,
                    address.IbgeCityCode,
                    address.Latitude,
                    address.Longitude))
                .ToArray());
}
