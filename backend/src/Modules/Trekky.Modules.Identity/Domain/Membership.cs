namespace Trekky.Modules.Identity.Domain;

/// <summary>A user's role in an organization. v0.1: exactly one per user, created at sign-up as Owner.</summary>
internal sealed class Membership
{
    public Guid OrgId { get; init; }

    public Guid UserId { get; init; }

    public string Role { get; set; } = Roles.Member;

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Membership roles, as stored and as sent in the <c>role</c> claim and the API.</summary>
internal static class Roles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Viewer = "viewer";
}
