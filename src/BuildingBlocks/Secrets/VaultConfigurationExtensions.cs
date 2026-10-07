using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace DistributedCommerce.Secrets;

public static class VaultConfigurationExtensions
{
    public static async Task AddVaultSecretsAsync(
        this ConfigurationManager configuration,
        CancellationToken cancellationToken = default)
    {
        var settings = VaultSettings.From(configuration);

        if (!settings.IsConfigured)
            return;

        var token = await ReadTokenAsync(settings.TokenFile!, cancellationToken);

        using var client = CreateClient(settings.Address!, token);
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(settings.SecretPath))
        {
            await AddKeyValueSecretsAsync(
                client,
                settings.SecretPath,
                values,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(settings.DatabaseRole))
        {
            await AddDynamicDatabaseCredentialsAsync(
                client,
                settings,
                values,
                cancellationToken);
        }

        configuration.AddInMemoryCollection(values);
    }

    private static async Task AddKeyValueSecretsAsync(
        HttpClient client,
        string secretPath,
        IDictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"v1/secret/data/{secretPath.TrimStart('/')}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VaultKvResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Vault returned an empty KV secret response.");

        foreach (var pair in payload.Data.Data)
        {
            values[pair.Key.Replace("__", ":", StringComparison.Ordinal)] = pair.Value;
        }
    }

    private static async Task AddDynamicDatabaseCredentialsAsync(
        HttpClient client,
        VaultSettings settings,
        IDictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        settings.ValidateDatabaseSettings();

        using var response = await client.GetAsync(
            $"v1/database/creds/{settings.DatabaseRole}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VaultDatabaseCredentialResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Vault returned an empty database credential response.");

        if (!payload.LeaseId.StartsWith(
                $"database/creds/{settings.DatabaseRole}/",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Vault returned an unexpected database lease identifier.");
        }

        values[$"ConnectionStrings:{settings.DatabaseConnectionStringName}"] =
            BuildPostgreSqlConnectionString(settings, payload.Data);

        values["Vault:DatabaseLeaseId"] = payload.LeaseId;
        values["Vault:DatabaseLeaseDurationSeconds"] = payload.LeaseDuration.ToString();
        values["Vault:DatabaseLeaseRenewable"] = payload.Renewable.ToString();
    }

    private static string BuildPostgreSqlConnectionString(
        VaultSettings settings,
        VaultDatabaseCredential credentials)
    {
        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = settings.DatabaseHost!,
            ["Port"] = settings.DatabasePort!,
            ["Database"] = settings.DatabaseName!,
            ["Username"] = credentials.Username,
            ["Password"] = credentials.Password,
            ["Pooling"] = "true"
        };

        if (!string.IsNullOrWhiteSpace(settings.DatabaseRuntimeRole))
        {
            ValidatePostgreSqlRoleName(settings.DatabaseRuntimeRole);
            builder["Options"] = $"-c role={settings.DatabaseRuntimeRole}";
        }

        return builder.ConnectionString;
    }

    private static void ValidatePostgreSqlRoleName(string role)
    {
        if (role.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character != '_'))
        {
            throw new InvalidOperationException(
                "Vault:DatabaseRuntimeRole may contain only ASCII letters, digits and underscores.");
        }
    }

    private static HttpClient CreateClient(string address, string token)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri($"{address.TrimEnd('/')}/", UriKind.Absolute)
        };

        client.DefaultRequestHeaders.Add("X-Vault-Token", token);
        return client;
    }

    private static async Task<string> ReadTokenAsync(
        string tokenFile,
        CancellationToken cancellationToken)
    {
        var token = (await File.ReadAllTextAsync(tokenFile, cancellationToken)).Trim();

        return string.IsNullOrWhiteSpace(token)
            ? throw new InvalidOperationException("Vault token file is empty.")
            : token;
    }

    private sealed record VaultKvResponse(VaultKvData Data);

    private sealed record VaultKvData(Dictionary<string, string?> Data);

    private sealed record VaultDatabaseCredentialResponse(
        [property: JsonPropertyName("lease_id")] string LeaseId,
        [property: JsonPropertyName("lease_duration")] int LeaseDuration,
        [property: JsonPropertyName("renewable")] bool Renewable,
        [property: JsonPropertyName("data")] VaultDatabaseCredential Data);

    private sealed record VaultDatabaseCredential(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("password")] string Password);

    private sealed record VaultSettings(
        string? Address,
        string? TokenFile,
        string? SecretPath,
        string? DatabaseRole,
        string? DatabaseConnectionStringName,
        string? DatabaseHost,
        string? DatabasePort,
        string? DatabaseName,
        string? DatabaseRuntimeRole)
    {
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Address) &&
            !string.IsNullOrWhiteSpace(TokenFile);

        public static VaultSettings From(IConfiguration configuration) =>
            new(
                configuration["Vault:Address"],
                configuration["Vault:TokenFile"],
                configuration["Vault:SecretPath"],
                configuration["Vault:DatabaseRole"],
                configuration["Vault:DatabaseConnectionStringName"],
                configuration["Vault:DatabaseHost"],
                configuration["Vault:DatabasePort"],
                configuration["Vault:DatabaseName"],
                configuration["Vault:DatabaseRuntimeRole"]);

        public void ValidateDatabaseSettings()
        {
            if (string.IsNullOrWhiteSpace(DatabaseConnectionStringName) ||
                string.IsNullOrWhiteSpace(DatabaseHost) ||
                string.IsNullOrWhiteSpace(DatabasePort) ||
                string.IsNullOrWhiteSpace(DatabaseName))
            {
                throw new InvalidOperationException(
                    "Dynamic database credentials require Vault database connection-string, host, port and database settings.");
            }
        }
    }
}
