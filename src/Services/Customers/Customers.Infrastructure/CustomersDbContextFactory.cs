using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Customers.Infrastructure;

public sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MIGRATIONS_CONNECTION_STRING") ??
            "Host=localhost;Port=5435;Database=customers;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new CustomersDbContext(options);
    }
}
