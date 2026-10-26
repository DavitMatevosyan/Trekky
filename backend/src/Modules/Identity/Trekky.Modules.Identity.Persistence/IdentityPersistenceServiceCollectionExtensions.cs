using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Persistence.Registration;

namespace Trekky.Modules.Identity.Persistence;

internal static class IdentityPersistenceServiceCollectionExtensions
{
    /// <summary>The identity schema's DbContext (RLS-aware, ADR-0002) and the membership lookup used before a tenant is known.</summary>
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration, IdentityDbContext.Schema);
        services.AddScoped<MembershipReader>();
        return services;
    }
}
