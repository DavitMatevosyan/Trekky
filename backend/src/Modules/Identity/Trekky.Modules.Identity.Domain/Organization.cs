namespace Trekky.Modules.Identity.Domain;

/// <summary>A tenant. Row-level security keys this table on <c>id</c> (ADR-0002).</summary>
internal sealed class Organization
{
    public const int NameMaxLength = 80;

    public Guid Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
