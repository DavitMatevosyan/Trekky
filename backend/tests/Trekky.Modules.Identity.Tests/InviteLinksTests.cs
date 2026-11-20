using Microsoft.Extensions.Options;
using Trekky.Modules.Identity.Infrastructure;

namespace Trekky.Modules.Identity.Tests;

public sealed class InviteLinksTests
{
    [Fact]
    public void Links_go_to_the_web_apps_origin_with_the_token_in_the_fragment()
    {
        var identity = new IdentityModuleOptions();
        identity.WebClient.RedirectUris.Add("https://trekky.example.ts.net/auth/callback");

        var links = new InviteLinks(Options.Create(identity), Options.Create(new InviteOptions()));

        Assert.Equal("https://trekky.example.ts.net/invite#tok", links.AcceptUrl("tok"));
    }

    [Fact]
    public void A_configured_base_url_wins()
    {
        var identity = new IdentityModuleOptions();
        identity.WebClient.RedirectUris.Add("http://localhost:5173/auth/callback");

        var links = new InviteLinks(Options.Create(identity), Options.Create(new InviteOptions { LinkBaseUrl = "https://app.example/" }));

        Assert.Equal("https://app.example/invite#tok", links.AcceptUrl("tok"));
    }

    [Fact]
    public void Without_any_address_building_a_link_fails_loudly()
    {
        var links = new InviteLinks(Options.Create(new IdentityModuleOptions()), Options.Create(new InviteOptions()));

        Assert.Throws<InvalidOperationException>(() => links.AcceptUrl("tok"));
    }
}
