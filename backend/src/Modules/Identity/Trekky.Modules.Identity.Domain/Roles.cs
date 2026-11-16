namespace Trekky.Modules.Identity.Domain;

/// <summary>Membership roles, as stored and as sent in the <c>role</c> claim and the API.</summary>
internal static class Roles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Viewer = "viewer";
}
