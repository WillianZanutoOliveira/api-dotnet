using DistributedCommerce.Contracts;
using Inventory.Service;
using NUnit.Framework;
using Orders.Application;
using Orders.Domain;
using Orders.Infrastructure;
using Payments.Service;

namespace Architecture.Tests;

[TestFixture]
public sealed class LayerDependencyTests
{
    [Test]
    public void Orders_domain_does_not_depend_on_application_infrastructure_or_frameworks()
    {
        AssertDoesNotReference(
            typeof(Order).Assembly,
            "Orders.Application",
            "Orders.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "MassTransit");
    }

    [Test]
    public void Orders_application_does_not_depend_on_infrastructure_or_transport_frameworks()
    {
        AssertDoesNotReference(
            typeof(OrderService).Assembly,
            "Orders.Infrastructure",
            "Microsoft.EntityFrameworkCore",
            "MassTransit");
    }

    [Test]
    public void Shared_contracts_do_not_depend_on_service_implementations()
    {
        AssertDoesNotReference(
            typeof(OrderSubmitted).Assembly,
            "Orders.Domain",
            "Orders.Application",
            "Orders.Infrastructure",
            "Inventory.Service",
            "Payments.Service",
            "Notifications.Service",
            "MassTransit",
            "Microsoft.EntityFrameworkCore");
    }

    [Test]
    public void Business_services_do_not_reference_each_other_directly()
    {
        var serviceAssemblies = new[]
        {
            typeof(InventoryDbContext).Assembly,
            typeof(PaymentsDbContext).Assembly,
            typeof(Notifications.Service.Program).Assembly
        };

        var forbiddenServiceAssemblies = new[]
        {
            "Inventory.Service",
            "Payments.Service",
            "Notifications.Service",
            "Orders.Api",
            "Orders.Infrastructure"
        };

        foreach (var assembly in serviceAssemblies)
        {
            var forbidden = forbiddenServiceAssemblies
                .Where(name => !string.Equals(name, assembly.GetName().Name, StringComparison.Ordinal))
                .ToArray();

            AssertDoesNotReference(assembly, forbidden);
        }
    }

    private static void AssertDoesNotReference(
        System.Reflection.Assembly assembly,
        params string[] forbiddenAssemblyNames)
    {
        var references = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        var violations = forbiddenAssemblyNames
            .Where(references.Contains)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            violations,
            Is.Empty,
            $"{assembly.GetName().Name} contains forbidden references: {string.Join(", ", violations)}");
    }
}
