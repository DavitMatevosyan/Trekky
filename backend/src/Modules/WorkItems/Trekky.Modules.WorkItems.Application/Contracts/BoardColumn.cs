namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>One status on the board: <see cref="Count"/> is every item in it, even when <see cref="Items"/> holds only the latest.</summary>
public sealed record BoardColumn(string Status, int Count, IReadOnlyList<BoardCard> Items);
