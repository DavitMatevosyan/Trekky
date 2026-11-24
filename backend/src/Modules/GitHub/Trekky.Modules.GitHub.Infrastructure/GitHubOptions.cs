namespace Trekky.Modules.GitHub.Infrastructure;

/// <summary>
/// The Trekky GitHub App this server acts as (configuration section <c>GitHub</c>, ADR-0028). Set all of
/// <see cref="ClientId"/>, <see cref="ClientSecret"/>, <see cref="AppSlug"/> and a private key, or none: without them
/// GitHub features answer 503. The secrets come from configuration only (environment, Docker secrets, later Azure Key
/// Vault), never from appsettings in the repository.
/// </summary>
internal sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>The app's client id: the JWT issuer and the OAuth client for the install code.</summary>
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    /// <summary>The app's URL name: <c>https://github.com/apps/{slug}</c>.</summary>
    public string? AppSlug { get; set; }

    /// <summary>The app's private key as PEM text; or use <see cref="PrivateKeyFile"/>.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>A file holding the PEM private key (a Docker secret in production).</summary>
    public string? PrivateKeyFile { get; set; }

    /// <summary>GitHub's REST API; a fake one in tests.</summary>
    public Uri ApiUrl { get; set; } = new("https://api.github.com/");

    /// <summary>github.com itself: the install page and the OAuth code exchange.</summary>
    public Uri WebUrl { get; set; } = new("https://github.com/");

    /// <summary>The PEM text from <see cref="PrivateKey"/> or <see cref="PrivateKeyFile"/>, or <c>null</c>.</summary>
    public string? ReadPrivateKeyPem()
        => !string.IsNullOrWhiteSpace(PrivateKey) ? PrivateKey
            : !string.IsNullOrWhiteSpace(PrivateKeyFile) ? File.ReadAllText(PrivateKeyFile)
            : null;

    /// <summary>Every setting the app needs is present.</summary>
    public bool IsComplete
        => !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(ClientSecret)
            && !string.IsNullOrWhiteSpace(AppSlug)
            && (!string.IsNullOrWhiteSpace(PrivateKey) || !string.IsNullOrWhiteSpace(PrivateKeyFile));

    /// <summary>No setting is present: GitHub is simply off on this server.</summary>
    public bool IsEmpty
        => string.IsNullOrWhiteSpace(ClientId)
            && string.IsNullOrWhiteSpace(ClientSecret)
            && string.IsNullOrWhiteSpace(AppSlug)
            && string.IsNullOrWhiteSpace(PrivateKey)
            && string.IsNullOrWhiteSpace(PrivateKeyFile);
}
