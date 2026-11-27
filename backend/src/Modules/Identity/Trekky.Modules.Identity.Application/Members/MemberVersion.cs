namespace Trekky.Modules.Identity.Application.Members;

/// <summary>A member's ETag: their membership's row version, sent in the members list and expected back in <c>If-Match</c>.</summary>
internal static class MemberVersion
{
    public static string ETagFor(uint version) => $"\"{version}\"";

    /// <summary>
    /// Whether an <c>If-Match</c> header (RFC 9110 §13.1.1) accepts this version: <c>*</c>, or any listed tag equal to the
    /// ETag, ignoring a <c>W/</c> prefix (as for work items).
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
