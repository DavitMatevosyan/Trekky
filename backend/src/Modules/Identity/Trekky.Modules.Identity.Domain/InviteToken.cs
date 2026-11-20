using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Trekky.Modules.Identity.Domain;

/// <summary>
/// The secret in an invite link: 32 random bytes as base64url (43 characters). Only the SHA-256 hash is stored, so a
/// database copy cannot be turned into working links (ADR-0027).
/// </summary>
internal static class InviteToken
{
    public const int ByteLength = 32;

    /// <summary>The length of <see cref="New"/>'s output; anything else is not a token.</summary>
    public const int Length = 43;

    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteLength));

    public static byte[] Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return SHA256.HashData(Encoding.ASCII.GetBytes(token));
    }

    /// <summary>Has the shape of a token (length and alphabet); says nothing about whether it exists.</summary>
    public static bool IsWellFormed(string? token)
        => token is { Length: Length } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
