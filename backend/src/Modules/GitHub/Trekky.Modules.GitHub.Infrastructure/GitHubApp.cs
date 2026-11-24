using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Trekky.Modules.GitHub.Application.GitHub;

namespace Trekky.Modules.GitHub.Infrastructure;

/// <summary>
/// The configured GitHub App: whether there is one, its install page, and the short-lived JWT it signs in to GitHub
/// with (RS256, issuer = client id, issued 60 seconds back for clock drift, valid 9 minutes; GitHub allows 10).
/// The private key is loaded once and never leaves this class.
/// </summary>
internal sealed class GitHubApp : IGitHubApp, IDisposable
{
    private static readonly TimeSpan ClockDrift = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(9);

    private readonly RSA? _key;
    private readonly Lock _signing = new();
    private readonly TimeProvider _time;

    public GitHubApp(IOptions<GitHubOptions> options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        _time = time;
        Options = options.Value;
        if (!Options.IsComplete)
        {
            return;
        }

        _key = RSA.Create();
        _key.ImportFromPem(Options.ReadPrivateKeyPem());
        InstallUrl = new Uri(Options.WebUrl, $"apps/{Uri.EscapeDataString(Options.AppSlug!)}/installations/new").ToString();
    }

    public GitHubOptions Options { get; }

    public bool IsConfigured => _key is not null;

    public string? InstallUrl { get; }

    /// <summary>A JWT for the app's own endpoints (<c>/app/...</c>). Throws when the app is not configured.</summary>
    public string CreateJwt()
    {
        var key = _key ?? throw new InvalidOperationException("The GitHub App is not configured.");
        var now = _time.GetUtcNow();
        var header = Base64Url.EncodeToString("""{"alg":"RS256","typ":"JWT"}"""u8);
        var payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new JwtPayload(
            (now - ClockDrift).ToUnixTimeSeconds(), (now + Lifetime).ToUnixTimeSeconds(), Options.ClientId!)));
        var signingInput = $"{header}.{payload}";

        byte[] signature;
        lock (_signing)
        {
            signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    public void Dispose() => _key?.Dispose();

    private sealed record JwtPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("iat")] long IssuedAt,
        [property: System.Text.Json.Serialization.JsonPropertyName("exp")] long Expires,
        [property: System.Text.Json.Serialization.JsonPropertyName("iss")] string Issuer);
}
