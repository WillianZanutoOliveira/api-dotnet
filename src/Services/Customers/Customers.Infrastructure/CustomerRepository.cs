using Customers.Application;
using Customers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Customers.Infrastructure;

public sealed class CustomerRepository(CustomersDbContext dbContext) : ICustomerRepository
{
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken) =>
        await dbContext.Customers.AddAsync(customer, cancellationToken);

    public Task<Customer?> GetByIdAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .Include(customer => customer.Addresses)
            .SingleOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);

    public Task<Customer?> GetForUpdateAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers
            .Include(customer => customer.Addresses)
            .SingleOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);

    public Task<bool> DocumentExistsAsync(
        string normalizedDocument,
        Guid? excludingCustomerId,
        CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(
            customer =>
                customer.Document == normalizedDocument &&
                (!excludingCustomerId.HasValue || customer.Id != excludingCustomerId.Value),
            cancellationToken);

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(
        CustomerQuery query,
        CancellationToken cancellationToken)
    {
        var customers = dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = $"%{query.Search.Trim()}%";
            customers = customers.Where(customer =>
                EF.Functions.ILike(customer.DisplayName, search) ||
                (customer.LegalName != null && EF.Functions.ILike(customer.LegalName, search)) ||
                EF.Functions.ILike(customer.Email, search));
        }

        if (!string.IsNullOrWhiteSpace(query.Document))
            customers = customers.Where(customer => customer.Document.Contains(query.Document));

        if (!string.IsNullOrWhiteSpace(query.PersonType) &&
            Enum.TryParse<PersonType>(query.PersonType, true, out var personType))
        {
            customers = customers.Where(customer => customer.PersonType == personType);
        }

        var totalCount = await customers.CountAsync(cancellationToken);

        var items = await customers
            .Include(customer => customer.Addresses)
            .OrderBy(customer => customer.DisplayName)
            .ThenBy(customer => customer.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Remove(Customer customer) =>
        dbContext.Customers.Remove(customer);
}
