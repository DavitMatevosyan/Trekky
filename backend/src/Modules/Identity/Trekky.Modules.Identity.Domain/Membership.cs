namespace Trekky.Modules.Identity.Domain;

/// <summary>
/// A user's role in an organization: exactly one per user (unique user_id, TREK-46), created at sign-up as Owner or when
/// an invite is accepted. Removing a member (TREK-48) keeps the row with <see cref="RemovedAt"/> set, so their name still
/// shows on their past work in that organization.
/// </summary>
internal sealed class Membership
{
    /// <summary>The <c>entity_type</c> of member events (docs/tech/activity-events.md); the entity id is the user's id.</summary>
    public const string EntityType = "member";

    public Guid OrgId { get; init; }

    public Guid UserId { get; init; }

    public string Role { get; set; } = Roles.Member;

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the member was removed (TREK-48); <c>null</c> while they belong to the organization.</summary>
    public DateTimeOffset? RemovedAt { get; set; }

    /// <summary>Postgres <c>xmin</c>; the member's ETag for role changes.</summary>
    public uint Version { get; init; }
}
