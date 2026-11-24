using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Trekky.Modules.GitHub.Infrastructure;

namespace Trekky.Modules.GitHub.Tests;

public sealed class GitHubAppTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Without_settings_GitHub_is_off_and_has_no_install_page()
    {
        using var app = new GitHubApp(Options.Create(new GitHubOptions()), new FixedTimeProvider(Now));

        Assert.False(app.IsConfigured);
        Assert.Null(app.InstallUrl);
        Assert.Throws<InvalidOperationException>(app.CreateJwt);
    }

    [Fact]
    public void The_install_page_is_the_app_page_on_github()
    {
        using var key = RSA.Create(2048);
        using var app = Configured(key);

        Assert.True(app.IsConfigured);
        Assert.Equal("https://github.com/apps/trekky-docs/installations/new", app.InstallUrl);
    }

    [Fact]
    public void The_app_jwt_is_signed_with_the_private_key_for_the_client_id_and_lives_under_ten_minutes()
    {
        using var key = RSA.Create(2048);
        using var app = Configured(key);

        var parts = app.CreateJwt().Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Base64Url.DecodeFromChars(parts[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));
        using var header = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[0]));
        using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("Iv23.client", payload.RootElement.GetProperty("iss").GetString());
        var iat = payload.RootElement.GetProperty("iat").GetInt64();
        var exp = payload.RootElement.GetProperty("exp").GetInt64();
        Assert.Equal(Now.AddSeconds(-60).ToUnixTimeSeconds(), iat);
        Assert.True(exp - iat <= 600, "GitHub refuses JWTs that live longer than 10 minutes.");
        Assert.True(exp > Now.ToUnixTimeSeconds());
    }

    [Fact]
    public void Half_a_configuration_is_neither_complete_nor_empty()
    {
        var options = new GitHubOptions { ClientId = "Iv23.client", AppSlug = "trekky-docs" };

        Assert.False(options.IsComplete);
        Assert.False(options.IsEmpty);
        Assert.True(new GitHubOptions().IsEmpty);
    }

    private static GitHubApp Configured(RSA key)
        => new(
            Options.Create(new GitHubOptions
            {
                ClientId = "Iv23.client",
                ClientSecret = "secret",
                AppSlug = "trekky-docs",
                PrivateKey = key.ExportRSAPrivateKeyPem(),
            }),
            new FixedTimeProvider(Now));
}
