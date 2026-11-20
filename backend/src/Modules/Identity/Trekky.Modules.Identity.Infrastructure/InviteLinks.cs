using Microsoft.Extensions.Options;

namespace Trekky.Modules.Identity.Infrastructure;

/// <summary>
/// Builds invite links: <c>{web app origin}/invite#{token}</c> (ADR-0027). The token rides in the fragment, which browsers
/// never send to a server, so it stays out of access logs and Referer headers. The origin is <c>Invites:LinkBaseUrl</c>
/// when set, otherwise that of the web client's first redirect URI (the public address in production).
/// </summary>
internal sealed class InviteLinks(IOptions<IdentityModuleOptions> identity, IOptions<InviteOptions> invites)
{
    public const string Path = "/invite";

    public string AcceptUrl(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return $"{BaseUrl()}{Path}#{token}";
    }

    private string BaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(invites.Value.LinkBaseUrl))
        {
            return invites.Value.LinkBaseUrl.TrimEnd('/');
        }

        var redirect = identity.Value.WebClient.RedirectUris.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"{InviteOptions.SectionName}:LinkBaseUrl is not set and there is no " +
                $"{IdentityModuleOptions.SectionName}:WebClient:RedirectUris entry to take the web app's address from.");
        return new Uri(redirect, UriKind.Absolute).GetLeftPart(UriPartial.Authority);
    }
}
