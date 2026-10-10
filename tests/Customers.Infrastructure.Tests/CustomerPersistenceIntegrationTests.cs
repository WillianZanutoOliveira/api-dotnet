using Customers.Domain;
using Customers.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Customers.Infrastructure.Tests;

[TestFixture]
public sealed class CustomerPersistenceIntegrationTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task StartDatabaseAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("customers_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgres.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopDatabaseAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Test]
    public async Task Updating_customer_replaces_address_without_concurrency_failure()
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        var customer = Customer.Create(
            PersonType.Individual,
            "111.444.777-35",
            "Original Customer",
            null,
            new DateOnly(1990, 1, 1),
            null,
            null,
            null,
            "original@example.invalid",
            "44999990000",
            [PrimaryAddress("Original Street", "10")]);

        var originalAddressId = customer.Addresses.Single().Id;

        await using (var initialContext = new CustomersDbContext(options))
        {
            await initialContext.Database.MigrateAsync();
            await initialContext.Customers.AddAsync(customer);
            await initialContext.SaveChangesAsync();
        }

        await using (var updateContext = new CustomersDbContext(options))
        {
            var persisted = await updateContext.Customers
                .Include(item => item.Addresses)
                .SingleAsync(item => item.Id == customer.Id);

            persisted.Update(
                PersonType.Individual,
                "11144477735",
                "Updated Customer",
                null,
                new DateOnly(1990, 1, 1),
                null,
                null,
                null,
                "updated@example.invalid",
                "44999990000",
                [PrimaryAddress("Replacement Street", "20")],
                isActive: false);

            await updateContext.SaveChangesAsync();
        }

        await using (var verificationContext = new CustomersDbContext(options))
        {
            var updated = await verificationContext.Customers
                .AsNoTracking()
                .Include(item => item.Addresses)
                .SingleAsync(item => item.Id == customer.Id);

            Assert.Multiple(() =>
            {
                Assert.That(updated.DisplayName, Is.EqualTo("Updated Customer"));
                Assert.That(updated.IsActive, Is.False);
                Assert.That(updated.Addresses, Has.Count.EqualTo(1));
                Assert.That(updated.Addresses.Single().Street, Is.EqualTo("Replacement Street"));
                Assert.That(updated.Addresses.Single().Number, Is.EqualTo("20"));
                Assert.That(updated.Addresses.Single().IsPrimary, Is.True);
                Assert.That(updated.Addresses.Single().Id, Is.Not.EqualTo(originalAddressId));
            });
        }
    }

    private static AddressDetails PrimaryAddress(string street, string number) =>
        new(
            AddressType.Primary,
            true,
            "87000-000",
            street,
            number,
            null,
            "Centro",
            "Maringa",
            "PR",
            "4115200",
            null,
            null);
}
