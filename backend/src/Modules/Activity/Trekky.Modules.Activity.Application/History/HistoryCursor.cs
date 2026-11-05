using System.Buffers.Binary;
using System.Buffers.Text;

namespace Trekky.Modules.Activity.Application.History;

/// <summary>
/// Keyset position in a history, newest first: the last event's id. UUIDv7 event ids sort by time, so the id alone
/// orders the page. Ids are random within a millisecond, so two events of the same millisecond may come in either
/// order; paging still has no gaps or repeats. Opaque to clients (base64url), so the ordering can change without breaking the API.
/// </summary>
internal sealed record HistoryCursor(Guid EventId)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public string Encode() => Base64Url.EncodeToString(EventId.ToByteArray(bigEndian: true));

    public static bool TryDecode(string? value, out HistoryCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        Span<byte> bytes = stackalloc byte[16];
        if (!Base64Url.IsValid(value, out var length) || length != bytes.Length
            || Base64Url.DecodeFromChars(value, bytes) != bytes.Length)
        {
            return false;
        }

        var id = new Guid(bytes, bigEndian: true);
        if (id == Guid.Empty)
        {
            return false;
        }

        cursor = new HistoryCursor(id);
        return true;
    }
}
