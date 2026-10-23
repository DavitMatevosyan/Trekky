namespace Trekky.Modules.Identity;

/// <summary>The <c>Identity</c> configuration section.</summary>
internal sealed class IdentityModuleOptions
{
    public const string SectionName = "Identity";

    /// <summary>Use OpenIddict's development signing and encryption certificates. Development and tests only.</summary>
    public bool UseDevelopmentCertificates { get; set; }

    /// <summary>Allow the OAuth endpoints and the session cookie over plain HTTP. Development only.</summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>PKCS#12 certificate that signs tokens (published via JWKS). Required unless development certificates are used.</summary>
    public CertificateOptions? SigningCertificate { get; set; }

    /// <summary>PKCS#12 certificate that encrypts authorization codes and refresh tokens. Required unless development certificates are used.</summary>
    public CertificateOptions? EncryptionCertificate { get; set; }

    public WebClientOptions WebClient { get; set; } = new();
}

internal sealed class CertificateOptions
{
    public string Path { get; set; } = string.Empty;

    public string? Password { get; set; }
}

/// <summary>The React app's OAuth client (public client, authorization code + PKCE).</summary>
internal sealed class WebClientOptions
{
    public IList<string> RedirectUris { get; } = new List<string>();
}
