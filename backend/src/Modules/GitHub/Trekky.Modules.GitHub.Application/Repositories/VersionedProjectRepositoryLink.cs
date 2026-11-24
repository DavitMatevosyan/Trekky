using Trekky.Modules.GitHub.Application.Contracts;

namespace Trekky.Modules.GitHub.Application.Repositories;

/// <summary>A project's link with its row version, which the API sends as the ETag and expects back in <c>If-Match</c>.</summary>
internal sealed record VersionedProjectRepositoryLink(ProjectRepositoryLink Link, uint Version)
{
    public string ETag => ETagFor(Version);

    public static string ETagFor(uint version) => $"\"{version}\"";

    /// <summary>
    /// Whether an <c>If-Match</c> header (RFC 9110 §13.1.1) accepts this version: <c>*</c>, or any listed tag equal to
    /// the ETag, ignoring a <c>W/</c> prefix (as for work items).
    /// </summary>
    public static bool IfMatchAccepts(string ifMatch, uint version)
    {
        ArgumentNullException.ThrowIfNull(ifMatch);
        var etag = ETagFor(version);
        foreach (var part in ifMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var tag = part.StartsWith("W/", StringComparison.Ordinal) ? part[2..] : part;
            if (tag == "*" || string.Equals(tag, etag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
