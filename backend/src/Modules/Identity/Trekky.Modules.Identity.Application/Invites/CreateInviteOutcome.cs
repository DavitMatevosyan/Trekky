namespace Trekky.Modules.Identity.Application.Invites;

internal enum CreateInviteOutcome
{
    Created,

    /// <summary>The caller's role may not invite with this role (only an Owner invites Admins).</summary>
    RoleNotAllowed,

    /// <summary>The address already belongs to a member of this organization.</summary>
    AlreadyMember,

    /// <summary>The address belongs to someone who was removed from this organization (TREK-48); their account remains.</summary>
    FormerMember,

    /// <summary>The address already has an account, which belongs to another organization (one organization per user).</summary>
    EmailHasAccount,

    /// <summary>The organization already has an open invite for this address; resend it instead.</summary>
    AlreadyInvited,
}
