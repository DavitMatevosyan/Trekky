namespace Trekky.Api.Tests;

/// <summary>A repository on the fake GitHub: its branches, and the folders and files on every branch.</summary>
public sealed record FakeRepository(long Id, string FullName, bool Private, string[] Branches, string[] Folders, string[] Files);
