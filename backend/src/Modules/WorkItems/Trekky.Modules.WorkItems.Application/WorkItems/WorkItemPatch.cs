namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>The fields of an edit (US-07); read from the JSON merge patch by <see cref="WorkItemPatchReader"/>.</summary>
internal sealed record WorkItemPatch(
    PatchValue<string?> Type,
    PatchValue<string?> Title,
    PatchValue<string?> DescriptionMd,
    PatchValue<Guid?> ParentId);
