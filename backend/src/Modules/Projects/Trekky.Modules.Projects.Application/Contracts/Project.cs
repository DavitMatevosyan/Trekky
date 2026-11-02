namespace Trekky.Modules.Projects.Application.Contracts;

/// <summary>A project as the API returns it.</summary>
/// <param name="ItemCount">Number of work items in the project (US-05 AC3).</param>
public sealed record Project(Guid Id, string Key, string Name, int ItemCount, DateTimeOffset CreatedAt);
