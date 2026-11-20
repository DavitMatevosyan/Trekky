namespace Trekky.Modules.GitHub.Domain;

/// <summary>
/// An installation of the Trekky GitHub App that an organization connected (TREK-74, ADR-0028). Only GitHub's id and
/// the account it is installed on are kept; its access tokens are fetched when needed and never stored.
/// </summary>
internal sealed class GitHubInstallation
{
    public const string EntityType = "github_installation";

    /// <summary>GitHub logins are at most 39 characters; room to spare.</summary>
    public const int AccountLoginMaxLength = 100;

    public const int AccountTypeMaxLength = 20;

    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    /// <summary>GitHub's installation id. One installation belongs to at most one Trekky organization.</summary>
    public long InstallationId { get; init; }

    /// <summary>The GitHub user or organization the app is installed on.</summary>
    public string AccountLogin { get; init; } = string.Empty;

    /// <summary><c>User</c> or <c>Organization</c>, as GitHub sends it.</summary>
    public string AccountType { get; init; } = string.Empty;

    /// <summary>User id from the identity module (no foreign key across modules).</summary>
    public Guid ConnectedBy { get; init; }

    public DateTimeOffset ConnectedAt { get; init; }
}
