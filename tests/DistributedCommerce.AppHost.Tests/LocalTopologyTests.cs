using Aspire.Hosting.Testing;
using NUnit.Framework;

namespace DistributedCommerce.AppHost.Tests;

public sealed class LocalTopologyTests
{
    private static readonly TimeSpan ModelTimeout = TimeSpan.FromMinutes(2);

    [Test]
    [Category("Topology")]
    public async Task AppHost_Declares_Expected_Secure_Topology()
    {
        Environment.SetEnvironmentVariable(
            "ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE",
            "false");
        Environment.SetEnvironmentVariable(
            "ASPIRE_VERSION_CHECK_DISABLED",
            "true");

        using var cancellation = new CancellationTokenSource(ModelTimeout);

        await using var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.DistributedCommerce_AppHost>(
                cancellationToken: cancellation.Token);

        var resourceNames = appHost.Resources
            .Select(resource => resource.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expectedResourceNames = new[]
        {
            "api-gateway",
            "inventory-db",
            "inventory-migrator",
            "inventory-service",
            "keycloak",
            "notifications-service",
            "orders-api",
            "orders-db",
            "orders-migrator",
            "payments-db",
            "payments-migrator",
            "payments-service",
            "rabbitmq",
            "vault",
            "vault-init"
        };

        Assert.That(resourceNames, Is.EquivalentTo(expectedResourceNames));
        Assert.That(resourceNames, Has.Length.EqualTo(expectedResourceNames.Length));
    }
}
