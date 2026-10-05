using System.Net;
using Aspire.Hosting.Testing;
using NUnit.Framework;

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

        using var gatewayClient = app.CreateHttpClient("api-gateway", "http");
        using var healthResponse = await gatewayClient.GetAsync("/health", cancellation.Token);
        using var aliveResponse = await gatewayClient.GetAsync("/alive", cancellation.Token);

        using var ordersClient = app.CreateHttpClient("orders-api", "http");
        using var openApiResponse = await ordersClient.GetAsync("/openapi/v1.json", cancellation.Token);
        var openApiDocument = await openApiResponse.Content.ReadAsStringAsync(cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(healthResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(aliveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(openApiResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(openApiDocument, Does.Contain("\"openapi\""));
        });
    }
}
