using Microsoft.Extensions.Configuration;

namespace DistributedCommerce.Security;

public sealed record KeycloakOptions(
    string Authority,
    string Issuer,
    string Audience,
    string? MetadataAddress,
    bool RequireHttpsMetadata)
{
    public static KeycloakOptions From(IConfiguration configuration)
    {
        var authority = Required(configuration, "Keycloak:Authority").TrimEnd('/');
        var issuer = (configuration["Keycloak:Issuer"] ?? authority).TrimEnd('/');
        var audience = Required(configuration, "Keycloak:Audience");
        var metadataAddress = configuration["Keycloak:MetadataAddress"];

        var requireHttpsMetadata = true;
        if (bool.TryParse(configuration["Keycloak:RequireHttpsMetadata"], out var configuredValue))
            requireHttpsMetadata = configuredValue;

        return new KeycloakOptions(
            authority,
            issuer,
            audience,
            string.IsNullOrWhiteSpace(metadataAddress) ? null : metadataAddress,
            requireHttpsMetadata);
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"Missing required configuration '{key}'.")
            : configuration[key]!;
}
