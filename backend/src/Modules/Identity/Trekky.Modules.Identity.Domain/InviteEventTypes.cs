namespace Trekky.Modules.Identity.Domain;

/// <summary>Activity event types for invites (TREK-47, docs/tech/activity-events.md). Entity type <c>invite</c>.</summary>
internal static class InviteEventTypes
{
    /// <summary>An invite was created or resent (payload <c>resent</c>).</summary>
    public const string InviteSent = "InviteSent";

    public const string InviteRevoked = "InviteRevoked";

    /// <summary>Someone accepted an invite and joined the organization; the actor is the new member.</summary>
    public const string InviteAccepted = "InviteAccepted";
}
