namespace Trekky.Modules.Projects.Application.Contracts;

/// <summary>A project as the API returns it.</summary>
/// <param name="ItemCount">Number of work items. Always 0 until work items arrive (TRK-11).</param>
public sealed record Project(Guid Id, string Key, string Name, int ItemCount, DateTimeOffset CreatedAt);
