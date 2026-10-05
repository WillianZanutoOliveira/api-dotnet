using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace DistributedCommerce.Secrets;

public static class VaultConfigurationExtensions
{
    public static async Task AddVaultSecretsAsync(
        this ConfigurationManager configuration,
        CancellationToken cancellationToken = default)
    {
        var address = configuration["Vault:Address"];
        var tokenFile = configuration["Vault:TokenFile"];
        var secretPath = configuration["Vault:SecretPath"];

        if (string.IsNullOrWhiteSpace(address) ||
            string.IsNullOrWhiteSpace(tokenFile) ||
            string.IsNullOrWhiteSpace(secretPath))
        {
            return;
        }

        var token = (await File.ReadAllTextAsync(tokenFile, cancellationToken)).Trim();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Vault token file is empty.");

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"{address.TrimEnd('/')}/", UriKind.Absolute)
        };

        client.DefaultRequestHeaders.Add("X-Vault-Token", token);

        using var response = await client.GetAsync(
            $"v1/secret/data/{secretPath.TrimStart('/')}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VaultKvResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Vault returned an empty secret response.");

        var values = payload.Data.Data.ToDictionary(
            pair => pair.Key.Replace("__", ":", StringComparison.Ordinal),
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        configuration.AddInMemoryCollection(values);
    }

    private sealed record VaultKvResponse(VaultKvData Data);

    private sealed record VaultKvData(Dictionary<string, string?> Data);
}
