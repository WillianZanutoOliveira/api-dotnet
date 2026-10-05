using Microsoft.Extensions.DependencyInjection;

namespace DistributedCommerce.Secrets;

public static class VaultServiceCollectionExtensions
{
    public static IServiceCollection AddVaultLeaseRenewal(this IServiceCollection services)
    {
        services.AddHttpClient(VaultLeaseRenewalService.HttpClientName);
        services.AddHostedService<VaultLeaseRenewalService>();

        return services;
    }
}
