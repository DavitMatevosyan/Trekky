namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>The desktop app's OAuth client (ADR-0017); without redirect URIs it uses <see cref="DesktopClient.DefaultRedirectUri"/>.</summary>
internal sealed class DesktopClientOptions
{
    public IList<string> RedirectUris { get; } = new List<string>();
}
