namespace Trekky.Modules.Identity.Domain;

/// <summary>A user's role in an organization: exactly one per user (unique user_id, TREK-46), created at sign-up as Owner.</summary>
internal sealed class Membership
{
    public Guid OrgId { get; init; }

    public Guid UserId { get; init; }

    public string Role { get; set; } = Roles.Member;

    public DateTimeOffset CreatedAt { get; init; }
}
