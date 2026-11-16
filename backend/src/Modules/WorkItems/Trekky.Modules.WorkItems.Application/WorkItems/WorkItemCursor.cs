using System.Buffers.Text;
using System.Text.Json;

namespace Trekky.Modules.WorkItems.Application.WorkItems;

/// <summary>
/// Keyset position in a project's list, newest update first (US-08 AC2): the last item's update time and id. Opaque to
/// clients (base64url JSON), so the ordering can change without breaking the API.
/// </summary>
internal sealed record WorkItemCursor(DateTimeOffset UpdatedAt, Guid Id)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public string Encode()
        => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Wire(UpdatedAt, Id)));

    public static bool TryDecode(string? value, out WorkItemCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        try
        {
            var wire = JsonSerializer.Deserialize<Wire>(Base64Url.DecodeFromChars(value));
            if (wire is null || wire.U is not { } updatedAt || wire.I == Guid.Empty)
            {
                return false;
            }

            // UTC: Npgsql only writes timestamptz parameters with a zero offset.
            cursor = new WorkItemCursor(updatedAt.ToUniversalTime(), wire.I);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }

    private sealed record Wire(DateTimeOffset? U, Guid I);
}
