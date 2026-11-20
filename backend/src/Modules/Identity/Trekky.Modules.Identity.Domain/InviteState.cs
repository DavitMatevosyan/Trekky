namespace Trekky.Modules.Identity.Domain;

/// <summary>Where an invite stands at a given moment (TREK-47).</summary>
internal enum InviteState
{
    Pending,
    Expired,
    Accepted,
    Revoked,
}
