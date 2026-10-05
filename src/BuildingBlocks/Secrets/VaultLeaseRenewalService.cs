using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DistributedCommerce.Secrets;

internal sealed class VaultLeaseRenewalService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    ILogger<VaultLeaseRenewalService> logger) : BackgroundService
{
    internal const string HttpClientName = "vault-lease-renewal";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var leaseId = configuration["Vault:DatabaseLeaseId"];
        var tokenFile = configuration["Vault:TokenFile"];
        var address = configuration["Vault:Address"];

        if (string.IsNullOrWhiteSpace(leaseId) ||
            string.IsNullOrWhiteSpace(tokenFile) ||
            string.IsNullOrWhiteSpace(address) ||
            !bool.TryParse(configuration["Vault:DatabaseLeaseRenewable"], out var renewable) ||
            !renewable)
        {
            return;
        }

        var interval = ResolveRenewalInterval(configuration);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);

            try
            {
                await RenewLeaseAsync(
                    address,
                    tokenFile,
                    leaseId,
                    stoppingToken);

                logger.LogInformation("Vault database credential lease renewed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Vault database credential lease renewal failed. The host will stop to fail closed.",
                    exception);
            }
        }
    }

    private async Task RenewLeaseAsync(
        string address,
        string tokenFile,
        string leaseId,
        CancellationToken cancellationToken)
    {
        var token = (await File.ReadAllTextAsync(tokenFile, cancellationToken)).Trim();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Vault token file is empty during lease renewal.");

        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri($"{address.TrimEnd('/')}/", UriKind.Absolute);
        client.DefaultRequestHeaders.Remove("X-Vault-Token");
        client.DefaultRequestHeaders.Add("X-Vault-Token", token);

        using var response = await client.PostAsync(
            $"v1/sys/leases/renew/{leaseId}",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private static TimeSpan ResolveRenewalInterval(IConfiguration configuration)
    {
        if (int.TryParse(
                configuration["Vault:LeaseRenewalIntervalSeconds"],
                out var configuredSeconds) &&
            configuredSeconds > 0)
        {
            return TimeSpan.FromSeconds(configuredSeconds);
        }

        if (!int.TryParse(
                configuration["Vault:DatabaseLeaseDurationSeconds"],
                out var leaseDurationSeconds) ||
            leaseDurationSeconds <= 0)
        {
            throw new InvalidOperationException("Vault database lease duration is invalid.");
        }

        return TimeSpan.FromSeconds(Math.Max(30, leaseDurationSeconds / 2));
    }

}
