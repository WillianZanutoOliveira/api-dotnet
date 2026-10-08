namespace Customers.Application;

public sealed class CustomerNotFoundException(Guid customerId)
    : Exception($"Customer '{customerId}' was not found.");

public sealed class DuplicateCustomerDocumentException()
    : Exception("A customer with the same document already exists.");

public sealed class PostalCodeNotFoundException(string postalCode)
    : Exception($"Postal code '{postalCode}' was not found.");
