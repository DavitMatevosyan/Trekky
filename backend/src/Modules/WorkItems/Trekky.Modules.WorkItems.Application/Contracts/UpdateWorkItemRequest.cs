namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>
/// Body of <c>PATCH /api/v1/work-items/{workItemId}</c> (US-07), a JSON merge patch: only the fields sent change, and
/// <c>null</c> clears <see cref="DescriptionMd"/>, <see cref="ParentId"/> or <see cref="AssigneeId"/>. Describes the body for OpenAPI (every field
/// optional); the endpoint reads the JSON itself, because a record cannot tell a missing field from a <c>null</c> one.
/// </summary>
public sealed record UpdateWorkItemRequest
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public string? DescriptionMd { get; init; }

    public Guid? ParentId { get; init; }

    /// <summary>A member of the organization (US-13), or <c>null</c> for nobody.</summary>
    public Guid? AssigneeId { get; init; }
}
