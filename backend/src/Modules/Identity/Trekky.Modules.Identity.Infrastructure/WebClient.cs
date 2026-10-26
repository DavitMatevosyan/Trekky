using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>The React app's OAuth client: public, authorization code + PKCE, refresh tokens (ADR-0003).</summary>
internal static class WebClient
{
    public const string ClientId = "trekky-web";

    public static OpenIddictApplicationDescriptor CreateDescriptor(IEnumerable<string> redirectUris)
    {
        ArgumentNullException.ThrowIfNull(redirectUris);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = ClientTypes.Public,
            ApplicationType = ApplicationTypes.Web,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Trekky web app",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Revocation,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
        };

        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        return descriptor;
    }
}

/// <summary>Creates or updates the web client on startup, so redirect URIs follow configuration.</summary>
internal sealed class WebClientRegistration(IServiceProvider services, IOptions<IdentityModuleOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = WebClient.CreateDescriptor(options.Value.WebClient.RedirectUris);

        var existing = await applications.FindByClientIdAsync(WebClient.ClientId, cancellationToken);
        if (existing is null)
        {
            await applications.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await applications.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
