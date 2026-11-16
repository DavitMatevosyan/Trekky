using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>US-06 AC4: the parent rules table.</summary>
public sealed class ParentRulesTests
{
    public static TheoryData<string, string[]> Table => new()
    {
        { WorkItemTypes.Epic, [] },
        { WorkItemTypes.Story, [WorkItemTypes.Epic] },
        { WorkItemTypes.Task, [WorkItemTypes.Epic, WorkItemTypes.Story] },
        { WorkItemTypes.SubTask, [WorkItemTypes.Story, WorkItemTypes.Task] },
        { WorkItemTypes.Bug, [WorkItemTypes.Epic, WorkItemTypes.Story] },
        { WorkItemTypes.TechDebt, [WorkItemTypes.Epic] },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_type_allows_exactly_the_parents_in_the_table(string type, string[] allowed)
    {
        Assert.Equal(allowed, ParentRules.AllowedParentTypes(type));
        foreach (var parentType in WorkItemTypes.All)
        {
            Assert.Equal(allowed.Contains(parentType), ParentRules.Allows(type, parentType));
        }
    }

    [Fact]
    public void Only_a_SubTask_needs_a_parent()
    {
        Assert.False(ParentRules.Allows(WorkItemTypes.SubTask, parentType: null));
        Assert.All(WorkItemTypes.All.Where(t => t != WorkItemTypes.SubTask), type => Assert.True(ParentRules.Allows(type, parentType: null)));
    }

    [Fact]
    public void No_type_can_be_its_own_ancestor()
    {
        // Following allowed parents from any type must end; otherwise a chain of parents could loop.
        foreach (var type in WorkItemTypes.All)
        {
            var seen = new HashSet<string>();
            var frontier = new Queue<string>([type]);
            while (frontier.TryDequeue(out var current))
            {
                foreach (var parent in ParentRules.AllowedParentTypes(current))
                {
                    Assert.NotEqual(type, parent);
                    if (seen.Add(parent))
                    {
                        frontier.Enqueue(parent);
                    }
                }
            }
        }
    }
}
