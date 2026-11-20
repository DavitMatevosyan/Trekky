namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>The result of resending or revoking an invite.</summary>
internal enum InviteChangeOutcome
{
    Done,

    /// <summary>No such invite in the caller's organization.</summary>
    NotFound,

    /// <summary>The caller's role may not manage an invite with this role.</summary>
    RoleNotAllowed,

    /// <summary>The invite was already accepted or revoked.</summary>
    NotOpen,

    /// <summary>Someone else changed the invite (accepted, resent or withdrew it) at the same moment; nothing was written.</summary>
    Conflict,
}
