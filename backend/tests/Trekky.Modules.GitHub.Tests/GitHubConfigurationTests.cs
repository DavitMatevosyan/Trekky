using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trekky.Modules.GitHub.Application.GitHub;
using Trekky.Modules.GitHub.Infrastructure;

namespace Trekky.Modules.GitHub.Tests;

/// <summary>ADR-0027: GitHub settings are all or none; half of them, or a key that is not one, stop the API at start.</summary>
public sealed class GitHubConfigurationTests
{
    [Fact]
    public void Without_any_settings_GitHub_is_off()
    {
        using var provider = Build(new Dictionary<string, string?>());

        var app = provider.GetRequiredService<IGitHubApp>();

        Assert.False(app.IsConfigured);
        Assert.Null(app.InstallUrl);
    }

    [Fact]
    public void Empty_settings_from_the_deploy_compose_file_mean_off_too()
    {
        using var provider = Build(new Dictionary<string, string?>
        {
            ["GitHub:ClientId"] = "",
            ["GitHub:ClientSecret"] = "",
            ["GitHub:AppSlug"] = "",
            ["GitHub:PrivateKeyFile"] = "",
        });

        Assert.False(provider.GetRequiredService<IGitHubApp>().IsConfigured);
    }

    [Fact]
    public void Half_the_settings_are_refused()
    {
        using var provider = Build(new Dictionary<string, string?> { ["GitHub:ClientId"] = "Iv23.client", ["GitHub:AppSlug"] = "trekky" });

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GitHubOptions>>().Value);
        Assert.Contains("together, or none of them", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_private_key_that_is_not_a_PEM_RSA_key_is_refused()
    {
        using var provider = Build(Complete("not a key"));

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GitHubOptions>>().Value);
        Assert.Contains("private key", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_complete_configuration_turns_GitHub_on()
    {
        using var key = RSA.Create(2048);
        using var provider = Build(Complete(key.ExportRSAPrivateKeyPem()));

        Assert.True(provider.GetRequiredService<IGitHubApp>().IsConfigured);
    }

    private static Dictionary<string, string?> Complete(string privateKey)
        => new()
        {
            ["GitHub:ClientId"] = "Iv23.client",
            ["GitHub:ClientSecret"] = "secret",
            ["GitHub:AppSlug"] = "trekky",
            ["GitHub:PrivateKey"] = privateKey,
        };

    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddGitHubInfrastructure(configuration).BuildServiceProvider();
    }
}
