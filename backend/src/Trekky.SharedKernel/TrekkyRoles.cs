namespace Trekky.SharedKernel;

/// <summary>
/// The organization roles, as sent in the access token's <c>role</c> claim (ADR-0003). Each role can do everything
/// the roles after it can: Owner, then Admin, then Member, then Viewer (ADR-0026).
/// </summary>
public static class TrekkyRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Viewer = "viewer";

    /// <summary>Every role, highest first.</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly([Owner, Admin, Member, Viewer]);
}
