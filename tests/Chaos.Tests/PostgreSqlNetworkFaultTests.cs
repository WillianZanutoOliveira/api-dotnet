using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;

namespace Chaos.Tests;

[TestFixture]
public sealed class PostgreSqlNetworkFaultTests
{
    private const ushort ToxiproxyControlPort = 8474;
    private const ushort ToxiproxyPostgresPort = 8666;
    private const string PostgresAlias = "orders-postgres";

    private INetwork _network = null!;
    private PostgreSqlContainer _postgres = null!;
    private IContainer _toxiproxy = null!;
    private HttpClient _toxiproxyClient = null!;

    [OneTimeSetUp]
    public async Task StartInfrastructureAsync()
    {
        _network = new NetworkBuilder().Build();
        await _network.CreateAsync();

        _postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("orders_chaos")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithNetwork(_network)
            .WithNetworkAliases(PostgresAlias)
            .Build();

        _toxiproxy = new ContainerBuilder("ghcr.io/shopify/toxiproxy:2.12.0")
            .WithNetwork(_network)
            .WithPortBinding(ToxiproxyControlPort, true)
            .WithPortBinding(ToxiproxyPostgresPort, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request =>
                        request.ForPort(ToxiproxyControlPort).ForPath("/version")))
            .Build();

        await _postgres.StartAsync();
        await _toxiproxy.StartAsync();

        _toxiproxyClient = new HttpClient
        {
            BaseAddress = new Uri(
                $"http://{_toxiproxy.Hostname}:{_toxiproxy.GetMappedPublicPort(ToxiproxyControlPort)}/")
        };

        using var response = await _toxiproxyClient.PostAsJsonAsync(
            "proxies",
            new
            {
                name = "orders-postgres",
                listen = $"0.0.0.0:{ToxiproxyPostgresPort}",
                upstream = $"{PostgresAlias}:5432",
                enabled = true
            });

        response.EnsureSuccessStatusCode();

        var directOptions = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var directContext = new OrdersDbContext(directOptions);
        await directContext.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task StopInfrastructureAsync()
    {
        _toxiproxyClient?.Dispose();

        if (_toxiproxy is not null)
            await _toxiproxy.DisposeAsync();

        if (_postgres is not null)
            await _postgres.DisposeAsync();

        if (_network is not null)
            await _network.DisposeAsync();
    }

    [Test]
    public async Task Database_connectivity_fails_during_network_cut_and_recovers_after_proxy_restore()
    {
        Assert.That(await CanConnectThroughProxyAsync(), Is.True);

        await SetProxyEnabledAsync(enabled: false);

        Assert.That(await CanConnectThroughProxyAsync(), Is.False);

        await SetProxyEnabledAsync(enabled: true);

        var recovered = false;

        for (var attempt = 0; attempt < 10 && !recovered; attempt++)
        {
            recovered = await CanConnectThroughProxyAsync();

            if (!recovered)
                await Task.Delay(250);
        }

        Assert.That(recovered, Is.True);
    }

    private async Task<bool> CanConnectThroughProxyAsync()
    {
        var connectionString =
            $"Host={_toxiproxy.Hostname};" +
            $"Port={_toxiproxy.GetMappedPublicPort(ToxiproxyPostgresPort)};" +
            "Database=orders_chaos;" +
            "Username=postgres;" +
            "Password=postgres;" +
            "Pooling=false;" +
            "Timeout=2;" +
            "Command Timeout=2";

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new OrdersDbContext(options);
        return await context.Database.CanConnectAsync();
    }

    private async Task SetProxyEnabledAsync(bool enabled)
    {
        using var response = await _toxiproxyClient.PostAsJsonAsync(
            "proxies/orders-postgres",
            new
            {
                name = "orders-postgres",
                listen = $"0.0.0.0:{ToxiproxyPostgresPort}",
                upstream = $"{PostgresAlias}:5432",
                enabled
            });

        response.EnsureSuccessStatusCode();
    }
}
