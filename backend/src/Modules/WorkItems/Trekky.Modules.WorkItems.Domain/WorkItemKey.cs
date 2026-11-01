using System.Globalization;
using System.Text.RegularExpressions;

namespace Trekky.Modules.WorkItems.Domain;

/// <summary>A work item's human key, <c>TRK-42</c>: the project key, a dash and the item's number in the project (US-06 AC2).</summary>
internal static partial class WorkItemKey
{
    public static string Format(string projectKey, int number)
        => string.Create(CultureInfo.InvariantCulture, $"{projectKey}-{number}");

    /// <summary>Splits <c>TRK-42</c> into <c>TRK</c> and 42; false for anything else, including numbers too large for an int.</summary>
    public static bool TryParse(string? key, out string projectKey, out int number)
    {
        projectKey = string.Empty;
        number = 0;
        var match = key is null ? Match.Empty : KeyRegex().Match(key);
        if (!match.Success
            || !int.TryParse(match.Groups["number"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out number))
        {
            return false;
        }

        projectKey = match.Groups["project"].Value;
        return true;
    }

    // \A and \z, not ^ and $: .NET's $ also matches before a trailing newline.
    [GeneratedRegex(@"\A(?<project>[A-Z][A-Z0-9]{1,9})-(?<number>[1-9][0-9]{0,9})\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}
