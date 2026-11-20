namespace Trekky.Modules.Identity.Application.Invites;

/// <summary>What an invite link leads to when it is opened or accepted.</summary>
internal enum InviteLinkOutcome
{
    Valid,

    /// <summary>No invite has this token: mistyped, or replaced by a resend.</summary>
    NotFound,

    Expired,

    Revoked,

    /// <summary>Already accepted through this link.</summary>
    Used,

    /// <summary>Accepting: the address got an account in the meantime (one organization per user).</summary>
    EmailHasAccount,
}
