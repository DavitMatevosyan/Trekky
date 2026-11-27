namespace Trekky.Modules.Identity.Application.Members;

/// <summary>The result of changing a member's role or removing them (TREK-48).</summary>
internal enum MemberChangeOutcome
{
    Done,

    /// <summary>No such member in the caller's organization (never was, or already removed).</summary>
    NotFound,

    /// <summary>The caller's role may not make this change: only an Owner makes or removes Admins and Owners.</summary>
    RoleNotAllowed,

    /// <summary>The member is the organization's only Owner, who can be neither demoted nor removed.</summary>
    LastOwner,

    /// <summary><c>If-Match</c> does not match the member's current version; the result carries the latest.</summary>
    Stale,
}
