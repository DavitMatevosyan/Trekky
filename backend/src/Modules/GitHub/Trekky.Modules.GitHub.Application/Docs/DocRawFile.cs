namespace Trekky.Modules.GitHub.Application.Docs;

/// <summary>An image from the docs folder, served as is.</summary>
internal sealed record DocRawFile(byte[] Bytes, string ContentType);
