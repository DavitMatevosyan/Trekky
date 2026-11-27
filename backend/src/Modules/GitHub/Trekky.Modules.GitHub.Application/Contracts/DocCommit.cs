namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>A commit shown as a doc's or the docs folder's last change (TREK-75).</summary>
/// <param name="Message">The first line of the commit message.</param>
/// <param name="Author">The git author's name.</param>
/// <param name="HtmlUrl">The commit on GitHub.</param>
public sealed record DocCommit(string Sha, string Message, string Author, DateTimeOffset CommittedAt, string HtmlUrl);
