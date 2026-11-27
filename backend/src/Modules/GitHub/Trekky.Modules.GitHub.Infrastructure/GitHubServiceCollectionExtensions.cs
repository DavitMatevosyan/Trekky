using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Trekky.Modules.GitHub.Application.Connections;
using Trekky.Modules.GitHub.Application.Docs;
using Trekky.Modules.GitHub.Application.GitHub;
using Trekky.Modules.GitHub.Application.Repositories;
using Trekky.Modules.GitHub.Persistence;

namespace Trekky.Modules.GitHub.Infrastructure;

internal static class GitHubServiceCollectionExtensions
{
    /// <summary>GitHub answers slower than the database; a request waiting longer than this fails with 502.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    public static IServiceCollection AddGitHubInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddGitHubPersistence(configuration);

        // Half a configuration is a mistake, not "off": the API refuses to start instead of failing on first use.
        services.AddOptions<GitHubOptions>()
            .Bind(configuration.GetSection(GitHubOptions.SectionName))
            .Validate(
                options => options.IsEmpty || options.IsComplete,
                "GitHub: set ClientId, ClientSecret, AppSlug and PrivateKey (or PrivateKeyFile) together, or none of them.")
            .Validate(options => options.IsEmpty || HasReadableKey(options), "GitHub: the private key is not a readable PEM RSA key.")
            .ValidateOnStart();

        services.AddSingleton<GitHubApp>();
        services.AddSingleton<IGitHubApp>(provider => provider.GetRequiredService<GitHubApp>());
        services.AddSingleton<InstallationTokenCache>();
        services.AddHttpClient<IGitHubClient, GitHubClient>(http =>
        {
            http.Timeout = RequestTimeout;
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Trekky", "1.0"));
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });

        services.AddScoped<IGitHubConnectionService, GitHubConnectionService>();
        services.AddScoped<IProjectRepositoryService, ProjectRepositoryService>();
        services.AddScoped<IDocsService, DocsService>();
        return services;
    }

    private static bool HasReadableKey(GitHubOptions options)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(options.ReadPrivateKeyPem());
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
