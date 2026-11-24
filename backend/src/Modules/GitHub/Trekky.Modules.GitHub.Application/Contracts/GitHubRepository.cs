namespace Trekky.Modules.GitHub.Application.Contracts;

/// <summary>A repository an installation can reach, for the project settings' picker.</summary>
/// <param name="FullName"><c>owner/name</c>.</param>
public sealed record GitHubRepository(long Id, string FullName, string DefaultBranch, bool Private, string HtmlUrl);
