using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Orders.Domain;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;

namespace Orders.Persistence.IntegrationTests;

[TestFixture]
public sealed class OrderRepositoryTests
{
    private PostgreSqlContainer _postgres = null!;

    [OneTimeSetUp]
    public async Task StartDatabaseAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("orders_test")
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
    public async Task Repository_persists_and_reads_order_from_real_postgresql()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var dbContext = new OrdersDbContext(options);
        await dbContext.Database.MigrateAsync();

        var repository = new OrderRepository(dbContext);
        var order = Order.Create("subject-integration-test", 125.50m);

        await repository.AddAsync(order, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(order.Id, CancellationToken.None);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.CustomerId, Is.EqualTo("subject-integration-test"));
        Assert.That(loaded.TotalAmount, Is.EqualTo(125.50m));
        Assert.That(loaded.Status, Is.EqualTo(OrderStatus.Pending));
    }
}
