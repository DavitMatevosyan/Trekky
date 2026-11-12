namespace Trekky.Modules.WorkItems.Domain;

/// <summary>A Markdown comment on a work item (US-12), flat: no threads in v1.0.</summary>
internal sealed class WorkItemComment
{
    public const int BodyMaxLength = 20_000;

    /// <summary>How much of the body a <c>CommentAdded</c> event carries (docs/tech/activity-events.md).</summary>
    public const int ExcerptLength = 200;

    public Guid Id { get; init; }

    public Guid OrgId { get; init; }

    public Guid WorkItemId { get; init; }

    /// <summary>User id from the identity module (no foreign key across modules).</summary>
    public Guid AuthorId { get; init; }

    public string BodyMd { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Equal to <see cref="CreatedAt"/> until the comment is edited (US-12 AC2).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Set when the author deletes the comment (soft delete, US-12 AC2); a deleted comment is never shown again.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Postgres <c>xmin</c>; sent to clients as the comment's ETag and checked against <c>If-Match</c> on edits.</summary>
    public uint Version { get; init; }

    /// <summary>The start of <paramref name="bodyMd"/> for an event, never cutting a character in two.</summary>
    public static string Excerpt(string bodyMd)
    {
        ArgumentNullException.ThrowIfNull(bodyMd);
        if (bodyMd.Length <= ExcerptLength)
        {
            return bodyMd;
        }

        var length = char.IsHighSurrogate(bodyMd[ExcerptLength - 1]) ? ExcerptLength - 1 : ExcerptLength;
        return bodyMd[..length];
    }
}
