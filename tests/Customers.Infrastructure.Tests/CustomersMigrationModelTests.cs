using Customers.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Customers.Infrastructure.Tests;

[TestFixture]
public sealed class CustomersMigrationModelTests
{
    [Test]
    public void Migrations_snapshot_matches_the_current_customers_model()
    {
        var options = new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql("Host=localhost;Database=customers;Username=not_used;Password=not_used")
            .Options;

        using var context = new CustomersDbContext(options);

        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.That(snapshot, Is.Not.Null, "Customers migrations require a model snapshot.");

        var snapshotModel = snapshot!.Model;
        if (snapshotModel is IMutableModel mutableModel)
            snapshotModel = mutableModel.FinalizeModel();

        snapshotModel = context.GetService<IModelRuntimeInitializer>().Initialize(snapshotModel);

        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.That(
            differences,
            Is.Empty,
            "CustomersDbContext has pending migration changes: " +
            string.Join(", ", differences.Select(operation =>
                operation.GetType().Name + " " +
                string.Join("; ", operation.GetType().GetProperties()
                    .Where(property => property.Name is "Name" or "Table" or "ColumnType" or "Schema" or "NewName")
                    .Select(property => property.Name + "=" + property.GetValue(operation))))));
    }
}
