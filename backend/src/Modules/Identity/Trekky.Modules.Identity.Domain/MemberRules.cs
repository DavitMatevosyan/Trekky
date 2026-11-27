namespace Trekky.Modules.Identity.Domain;

/// <summary>
/// Who may change whose role or remove whom (TREK-48, ADR-0026): Admins manage Members and Viewers; only an Owner makes or
/// removes Admins and Owners. An organization always keeps at least one Owner.
/// </summary>
internal static class MemberRules
{
    /// <summary>Every role a member can have, highest first.</summary>
    public static IReadOnlyList<string> Assignable { get; } = Array.AsReadOnly([Roles.Owner, Roles.Admin, Roles.Member, Roles.Viewer]);

    public static bool IsRole(string? role) => role is not null && Assignable.Contains(role, StringComparer.Ordinal);

    /// <summary>Whether someone with <paramref name="actorRole"/> may move a member from <paramref name="currentRole"/> to <paramref name="newRole"/>.</summary>
    public static bool CanChangeRole(string? actorRole, string currentRole, string newRole)
        => IsRole(newRole) && Manages(actorRole, currentRole) && Manages(actorRole, newRole);

    /// <summary>Whether someone with <paramref name="actorRole"/> may remove a member with <paramref name="role"/>.</summary>
    public static bool CanRemove(string? actorRole, string role) => Manages(actorRole, role);

    /// <summary>
    /// Whether giving a member with <paramref name="currentRole"/> the role <paramref name="newRole"/> (<c>null</c>: removing
    /// them) leaves the organization without an Owner, given its <paramref name="owners"/> Owners.
    /// </summary>
    public static bool LeavesNoOwner(string currentRole, string? newRole, int owners)
        => currentRole == Roles.Owner && newRole != Roles.Owner && owners <= 1;

    private static bool Manages(string? actorRole, string role) => actorRole switch
    {
        Roles.Owner => true,
        Roles.Admin => role is Roles.Member or Roles.Viewer,
        _ => false,
    };
}
