using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DistributedCommerce.Security;

public static class SecurityPolicies
{
    public const string OrdersRead = "orders.read";
    public const string OrdersWrite = "orders.write";
    public const string PlatformAdmin = "platform.admin";
}

public static class KeycloakAuthenticationExtensions
{
    public static IServiceCollection AddPlatformIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keycloak = KeycloakOptions.From(configuration);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloak.Authority;
                options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
                options.MapInboundClaims = false;

                if (keycloak.MetadataAddress is not null)
                    options.MetadataAddress = keycloak.MetadataAddress;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidIssuer = keycloak.Issuer,
                    ValidateAudience = true,
                    ValidAudience = keycloak.Audience,
                    ValidateLifetime = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services
            .AddAuthorizationBuilder()
            .AddPolicy(
                SecurityPolicies.OrdersRead,
                policy => policy.RequireAuthenticatedUser().RequireRole("customer", "admin"))
            .AddPolicy(
                SecurityPolicies.OrdersWrite,
                policy => policy.RequireAuthenticatedUser().RequireRole("customer", "admin"))
            .AddPolicy(
                SecurityPolicies.PlatformAdmin,
                policy => policy.RequireAuthenticatedUser().RequireRole("admin"));

        return services;
    }
}
