namespace Trekky.Modules.Projects.Domain;

/// <summary>A project groups work items and gives them their key prefix (US-05). The key never changes.</summary>
internal sealed class Project
{
    public const int NameMaxLength = 80;

    /// <summary>2–10 characters: an uppercase letter, then uppercase letters or digits. POSIX form, for the database check.</summary>
    public const string KeyPattern = "^[A-Z][A-Z0-9]{1,9}$";

    /// <summary>
    /// <see cref="KeyPattern"/> for .NET: <c>\A</c>/<c>\z</c>, because .NET's <c>$</c> also matches before a trailing
    /// newline, which the database check would then reject with a 500.
    /// </summary>
    public const string KeyRegex = @"\A[A-Z][A-Z0-9]{1,9}\z";

    public const int KeyMaxLength = 10;

    public const string EntityType = "project";

    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>User id from the identity module (no foreign key across modules).</summary>
    public Guid CreatedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Postgres <c>xmin</c>; the ETag for future edits.</summary>
    public uint Version { get; init; }
}
