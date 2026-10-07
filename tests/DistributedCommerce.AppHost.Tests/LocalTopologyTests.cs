using System.Net;
using Aspire.Hosting.Testing;
using NUnit.Framework;

namespace DistributedCommerce.AppHost.Tests;

public sealed class LocalTopologyTests
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan ResourceTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    [Test]
    [Category("Topology")]
    public async Task AppHost_Starts_Orders_And_Gateway()
    {
        Environment.SetEnvironmentVariable(
            "ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE",
            "false");
        Environment.SetEnvironmentVariable(
            "ASPIRE_VERSION_CHECK_DISABLED",
            "true");

        using var buildCancellation = new CancellationTokenSource(BuildTimeout);

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.DistributedCommerce_AppHost>(
                cancellationToken: buildCancellation.Token);

        await using var app = await appHost
            .BuildAsync(buildCancellation.Token)
            .WaitAsync(BuildTimeout, buildCancellation.Token);

        using var startupCancellation = new CancellationTokenSource(StartupTimeout);

        await app
            .StartAsync(startupCancellation.Token)
            .WaitAsync(StartupTimeout, startupCancellation.Token);

        using var resourceCancellation = new CancellationTokenSource(ResourceTimeout);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("orders-api", resourceCancellation.Token)
            .WaitAsync(ResourceTimeout, resourceCancellation.Token);

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("api-gateway", resourceCancellation.Token)
            .WaitAsync(ResourceTimeout, resourceCancellation.Token);

        using var requestCancellation = new CancellationTokenSource(RequestTimeout);
        using var gatewayClient = app.CreateHttpClient("api-gateway", "http");
        using var healthResponse = await gatewayClient.GetAsync("/health", requestCancellation.Token);
        using var aliveResponse = await gatewayClient.GetAsync("/alive", requestCancellation.Token);

        using var ordersClient = app.CreateHttpClient("orders-api", "http");
        using var openApiResponse = await ordersClient.GetAsync("/openapi/v1.json", requestCancellation.Token);
        var openApiDocument = await openApiResponse.Content.ReadAsStringAsync(requestCancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(healthResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(aliveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(openApiResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(openApiDocument, Does.Contain("\"openapi\""));
        });
    }
}
