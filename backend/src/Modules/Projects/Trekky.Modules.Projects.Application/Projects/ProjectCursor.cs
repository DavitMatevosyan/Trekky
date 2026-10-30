using System.Buffers.Text;
using System.Text.Json;

namespace Trekky.Modules.Projects.Application.Projects;

/// <summary>
/// Keyset position in the name-ordered project list: the last item's name and id. Opaque to clients
/// (base64url JSON), so the ordering can change without breaking the API.
/// </summary>
internal sealed record ProjectCursor(string Name, Guid Id)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public string Encode()
        => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Wire(Name, Id)));

    public static bool TryDecode(string? value, out ProjectCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        try
        {
            var wire = JsonSerializer.Deserialize<Wire>(Base64Url.DecodeFromChars(value));
            if (wire is null || wire.N is null || wire.I == Guid.Empty)
            {
                return false;
            }

            cursor = new ProjectCursor(wire.N, wire.I);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return false;
        }
    }

    private sealed record Wire(string? N, Guid I);
}
