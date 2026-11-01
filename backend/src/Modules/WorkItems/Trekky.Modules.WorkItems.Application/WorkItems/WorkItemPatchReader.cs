using System.Text.Json;
using System.Text.Json.Nodes;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>
/// Reads a JSON merge patch (RFC 7396) for a work item: which fields were sent, and whether each has the right JSON
/// type. Values are then checked by <see cref="WorkItemPatchValidator"/>. Unknown fields are rejected, so a typo or a
/// field that is not editable here (status, TRK-12) is not silently ignored.
/// </summary>
internal static class WorkItemPatchReader
{
    public const string BodyField = "body";

    private const string TypeField = "type";
    private const string TitleField = "title";
    private const string DescriptionField = "descriptionMd";
    private const string ParentField = "parentId";

    public static bool TryRead(JsonNode? body, out WorkItemPatch patch, out Dictionary<string, string[]> errors)
    {
        var type = PatchValue<string?>.Unset;
        var title = PatchValue<string?>.Unset;
        var description = PatchValue<string?>.Unset;
        var parent = PatchValue<Guid?>.Unset;
        errors = [];

        if (body is not JsonObject fields)
        {
            errors[BodyField] = ["The body must be a JSON object."];
        }
        else if (fields.Count == 0)
        {
            errors[BodyField] = ["Send at least one field to change."];
        }
        else
        {
            foreach (var (name, value) in fields)
            {
                switch (name)
                {
                    case TypeField:
                        type = ReadString(value, name, "Type", nullable: false, errors);
                        break;
                    case TitleField:
                        title = ReadString(value, name, "Title", nullable: false, errors);
                        break;
                    case DescriptionField:
                        description = ReadString(value, name, "Description", nullable: true, errors);
                        break;
                    case ParentField:
                        parent = ReadGuid(value, name, errors);
                        break;
                    default:
                        errors[name] = ["This field does not exist or cannot be changed here."];
                        break;
                }
            }
        }

        patch = new WorkItemPatch(type, title, description, parent);
        return errors.Count == 0;
    }

    private static PatchValue<string?> ReadString(
        JsonNode? value, string name, string label, bool nullable, Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            if (!nullable)
            {
                errors[name] = [$"{label} cannot be null."];
            }

            return PatchValue<string?>.Set(null);
        }

        if (value.GetValueKind() != JsonValueKind.String)
        {
            errors[name] = [$"{label} must be a string."];
            return PatchValue<string?>.Unset;
        }

        return PatchValue<string?>.Set(value.GetValue<string>());
    }

    private static PatchValue<Guid?> ReadGuid(JsonNode? value, string name, Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            return PatchValue<Guid?>.Set(null);
        }

        if (value.GetValueKind() == JsonValueKind.String && Guid.TryParse(value.GetValue<string>(), out var id))
        {
            return PatchValue<Guid?>.Set(id);
        }

        errors[name] = ["Parent must be a work item id or null."];
        return PatchValue<Guid?>.Unset;
    }
}
