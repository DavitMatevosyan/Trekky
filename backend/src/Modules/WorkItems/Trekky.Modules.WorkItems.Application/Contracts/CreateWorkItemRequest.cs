namespace Trekky.Modules.WorkItems.Application.Contracts;

/// <summary>Body of <c>POST /api/v1/projects/{projectId}/work-items</c> (US-06).</summary>
/// <param name="Type"><c>epic</c>, <c>story</c>, <c>task</c>, <c>subtask</c>, <c>bug</c> or <c>tech_debt</c>.</param>
/// <param name="DescriptionMd">Markdown, optional.</param>
/// <param name="ParentId">An item of the same project; required for a SubTask.</param>
public sealed record CreateWorkItemRequest(string? Type, string? Title, string? DescriptionMd = null, Guid? ParentId = null);
