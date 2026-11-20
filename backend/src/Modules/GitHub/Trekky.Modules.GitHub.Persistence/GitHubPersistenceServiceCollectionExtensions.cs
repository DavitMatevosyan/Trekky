using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trekky.Persistence.Outbox;
using Trekky.Persistence.Registration;

namespace Trekky.Modules.GitHub.Persistence;

internal static class GitHubPersistenceServiceCollectionExtensions
{
    /// <summary>The github schema's DbContext (RLS-aware, ADR-0002) and its outbox (drained into the activity log).</summary>
    public static IServiceCollection AddGitHubPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<GitHubDbContext>(configuration, GitHubDbContext.Schema);
        services.AddModuleOutbox(GitHubDbContext.Schema);
        return services;
    }
}
