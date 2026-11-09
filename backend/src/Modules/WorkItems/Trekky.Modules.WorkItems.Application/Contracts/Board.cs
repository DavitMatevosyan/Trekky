namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>A project's board (US-11): one column per status, in workflow order.</summary>
public sealed record Board(IReadOnlyList<BoardColumn> Columns);
