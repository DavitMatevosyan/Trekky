using System.Collections.Frozen;

namespace Trekky.SharedKernel;

/// <summary>
/// The authorization policies every endpoint picks from, one per kind of action (ADR-0025). Each names the roles it
/// lets through; the API checks them, so hiding a button in the UI is never the only guard.
/// </summary>
public static class TrekkyPolicies
{
    /// <summary>Read projects, work items, the board, comments, history and members. Every role.</summary>
    public const string ReadWork = "ReadWork";

    /// <summary>Create and edit work items, move them, and add, edit or delete one's own comments. Member and up.</summary>
    public const string EditWork = "EditWork";

    /// <summary>Create projects. Admin and up.</summary>
    public const string ManageProjects = "ManageProjects";

    /// <summary>Invite, change and remove members (TREK-47, TREK-48). Admin and up; only an Owner manages Admins and Owners.</summary>
    public const string ManageMembers = "ManageMembers";

    /// <summary>The roles each policy lets through.</summary>
    public static FrozenDictionary<string, IReadOnlyList<string>> Roles { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [ReadWork] = TrekkyRoles.All,
        [EditWork] = [TrekkyRoles.Owner, TrekkyRoles.Admin, TrekkyRoles.Member],
        [ManageProjects] = [TrekkyRoles.Owner, TrekkyRoles.Admin],
        [ManageMembers] = [TrekkyRoles.Owner, TrekkyRoles.Admin],
    }.ToFrozenDictionary();
}
