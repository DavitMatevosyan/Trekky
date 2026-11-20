namespace Trekky.Modules.Identity.Domain;

/// <summary>
/// Which roles an invite can carry, and who may send which (TREK-47, ADR-0026): Admins invite Members and Viewers;
/// only an Owner invites Admins. Nobody is invited as Owner: ownership is given to an existing member (TREK-48).
/// </summary>
internal static class InviteRoles
{
    public static IReadOnlyList<string> Invitable { get; } = Array.AsReadOnly([Roles.Admin, Roles.Member, Roles.Viewer]);

    public static bool IsInvitable(string? role) => role is not null && Invitable.Contains(role, StringComparer.Ordinal);

    /// <summary>Whether someone with <paramref name="inviterRole"/> may send, resend or revoke an invite for <paramref name="role"/>.</summary>
    public static bool CanManage(string? inviterRole, string role)
        => IsInvitable(role) && inviterRole switch
        {
            Roles.Owner => true,
            Roles.Admin => role != Roles.Admin,
            _ => false,
        };
}
