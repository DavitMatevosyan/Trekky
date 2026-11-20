namespace Trekky.Modules.GitHub.Application.GitHub;

/// <summary>Links to github.com pages shown in the UI.</summary>
internal static class GitHubUrls
{
    private const string Site = "https://github.com";

    /// <summary>The installation's settings page, where its repositories are chosen and the app is uninstalled.</summary>
    public static string Installation(long installationId, string accountLogin, string accountType)
        => string.Equals(accountType, "Organization", StringComparison.Ordinal)
            ? $"{Site}/organizations/{Uri.EscapeDataString(accountLogin)}/settings/installations/{installationId}"
            : $"{Site}/settings/installations/{installationId}";

    /// <summary>The docs folder on the branch: <c>https://github.com/acme/handbook/tree/dev/docs</c>.</summary>
    public static string Folder(string repositoryFullName, string branch, string docsPath)
    {
        var url = $"{Site}/{repositoryFullName}/tree/{EscapeSegments(branch)}";
        var folder = docsPath.TrimEnd('/');
        return folder.Length == 0 ? url : $"{url}/{EscapeSegments(folder)}";
    }

    /// <summary>Escapes each segment of a slash-separated path, keeping the slashes.</summary>
    public static string EscapeSegments(string path)
        => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
