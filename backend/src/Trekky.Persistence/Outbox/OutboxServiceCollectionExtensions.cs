using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Trekky.Persistence.Rls;

namespace Trekky.Persistence.Outbox;

public static class OutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the module's <c>{schema}.outbox</c> with the outbox dispatcher, and the dispatcher itself (once).
    /// Map the table with <see cref="OutboxModelExtensions.AddOutbox"/> and secure it with
    /// <see cref="OutboxRowLevelSecurity.EnableOutboxRowLevelSecurity"/>.
    /// </summary>
    public static IServiceCollection AddModuleOutbox(this IServiceCollection services, string schema)
    {
        TenantRowLevelSecurity.Identifier(schema, nameof(schema));
        services.AddSingleton(new OutboxSource(schema));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxDispatcher>());
        return services;
    }
}
