namespace Trekky.Modules.Projects.Application.Contracts;

/// <summary>What other modules need to know about a project: its id, immutable key and name.</summary>
public sealed record ProjectRef(Guid Id, string Key, string Name);
