using System.Buffers.Text;
using System.Text.Json;

namespace Trekky.Modules.WorkItems.Application.Comments;

/// <summary>
/// Keyset position in an item's comments, oldest first: the last comment's time and id. Opaque to clients (base64url
/// JSON), so the ordering can change without breaking the API.
/// </summary>
internal sealed record CommentCursor(DateTimeOffset CreatedAt, Guid Id)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public string Encode()
        => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Wire(CreatedAt, Id)));

    public static bool TryDecode(string? value, out CommentCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        try
        {
            var wire = JsonSerializer.Deserialize<Wire>(Base64Url.DecodeFromChars(value));
            if (wire is null || wire.C is not { } createdAt || wire.I == Guid.Empty)
            {
                return false;
            }

            // UTC: Npgsql only writes timestamptz parameters with a zero offset.
            cursor = new CommentCursor(createdAt.ToUniversalTime(), wire.I);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }

    private sealed record Wire(DateTimeOffset? C, Guid I);
}
