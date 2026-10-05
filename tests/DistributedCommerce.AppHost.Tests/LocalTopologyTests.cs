using System.Net;
using Aspire.Hosting.Testing;

namespace DistributedCommerce.AppHost.Tests;

public sealed class LocalTopologyTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);

    [Test]
    public async Task AppHost_Starts_Orders_And_Gateway()
    {
        Environment.SetEnvironmentVariable(
            "ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE",
            "false");
        Environment.SetEnvironmentVariable(
            "ASPIRE_VERSION_CHECK_DISABLED",
            "true");

        using var cancellation = new CancellationTokenSource(DefaultTimeout);

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.DistributedCommerce_AppHost>(
                cancellationToken: cancellation.Token);

        await using var app = await appHost
            .BuildAsync(cancellation.Token)
            .WaitAsync(DefaultTimeout, cancellation.Token);

        await app
            .StartAsync(cancellation.Token)
            .WaitAsync(DefaultTimeout, cancellation.Token);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("orders-api", cancellation.Token)
            .WaitAsync(DefaultTimeout, cancellation.Token);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("api-gateway", cancellation.Token)
            .WaitAsync(DefaultTimeout, cancellation.Token);

        using var client = app.CreateHttpClient("api-gateway", "http");
        using var response = await client.GetAsync("/health", cancellation.Token);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
