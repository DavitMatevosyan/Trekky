using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>Creates or updates the web and desktop clients on startup, so redirect URIs and settings follow the code and configuration.</summary>
internal sealed class ClientRegistration(IServiceProvider services, IOptions<IdentityModuleOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        await RegisterAsync(applications, WebClient.CreateDescriptor(options.Value.WebClient.RedirectUris), cancellationToken);
        await RegisterAsync(applications, DesktopClient.CreateDescriptor(options.Value.DesktopClient.RedirectUris), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task RegisterAsync(
        IOpenIddictApplicationManager applications, OpenIddictApplicationDescriptor descriptor, CancellationToken cancellationToken)
    {
        var existing = await applications.FindByClientIdAsync(descriptor.ClientId!, cancellationToken);
        if (existing is null)
        {
            await applications.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await applications.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }
}
