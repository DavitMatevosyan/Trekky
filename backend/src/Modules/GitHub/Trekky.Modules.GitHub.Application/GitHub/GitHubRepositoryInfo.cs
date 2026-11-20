namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>A repository as GitHub describes it.</summary>
internal sealed record GitHubRepositoryInfo(long Id, string FullName, string DefaultBranch, bool Private, string HtmlUrl);
