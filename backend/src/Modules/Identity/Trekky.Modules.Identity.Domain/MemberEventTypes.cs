namespace Trekky.Modules.Identity.Domain;

/// <summary>Activity event types for members (TREK-48, docs/tech/activity-events.md). Entity type <c>member</c>, id = the user's.</summary>
internal static class MemberEventTypes
{
    /// <summary>A member's role changed (payload <c>from</c>, <c>to</c>).</summary>
    public const string MemberRoleChanged = "MemberRoleChanged";

    /// <summary>A member was removed from the organization (payload <c>role</c>: the role they had).</summary>
    public const string MemberRemoved = "MemberRemoved";
}
