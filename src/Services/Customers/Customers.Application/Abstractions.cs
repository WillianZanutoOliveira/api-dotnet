using Customers.Domain;

namespace Customers.Application;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken);
    Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken);
    Task<bool> DocumentExistsAsync(
        string normalizedDocument,
        Guid? excludingCustomerId,
        CancellationToken cancellationToken);
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(
        CustomerQuery query,
        CancellationToken cancellationToken);
    void Remove(Customer customer);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPostalCodeLookup
{
    Task<PostalCodeLookupResult> LookupAsync(
        string postalCode,
        CancellationToken cancellationToken);
}
