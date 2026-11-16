using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Application.WorkItems;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>US-10: the fixed workflow's transitions and the status change request.</summary>
public sealed class StatusWorkflowTests
{
    public static TheoryData<string, string[]> Table => new()
    {
        { WorkItemStatuses.ToDo, [WorkItemStatuses.InProgress] },
        { WorkItemStatuses.InProgress, [WorkItemStatuses.ToDo, WorkItemStatuses.InReview] },
        { WorkItemStatuses.InReview, [WorkItemStatuses.ToDo, WorkItemStatuses.InProgress, WorkItemStatuses.Done] },
        { WorkItemStatuses.Done, [WorkItemStatuses.InProgress] },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_status_allows_exactly_the_moves_in_the_table(string from, string[] allowed)
    {
        Assert.Equal(allowed, StatusWorkflow.AllowedTransitions(from));
        foreach (var to in WorkItemStatuses.All)
        {
            Assert.Equal(allowed.Contains(to), StatusWorkflow.Allows(from, to));
        }
    }

    [Fact]
    public void Staying_in_the_same_status_is_not_a_move()
        => Assert.All(WorkItemStatuses.All, status => Assert.False(StatusWorkflow.Allows(status, status)));

    [Fact]
    public void Every_status_can_be_reached_and_left()
    {
        foreach (var status in WorkItemStatuses.All)
        {
            Assert.NotEmpty(StatusWorkflow.AllowedTransitions(status));
            Assert.Contains(WorkItemStatuses.All, from => StatusWorkflow.Allows(from, status));
        }
    }

    [Fact]
    public void An_unknown_status_allows_no_moves()
    {
        Assert.Empty(StatusWorkflow.AllowedTransitions("blocked"));
        Assert.False(StatusWorkflow.Allows("blocked", WorkItemStatuses.ToDo));
    }

    [Theory]
    [InlineData("todo")]
    [InlineData("in_progress")]
    [InlineData("in_review")]
    [InlineData("done")]
    public void The_request_accepts_every_status(string to)
        => Assert.True(new ChangeStatusRequestValidator().Validate(new ChangeStatusRequest(to)).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Done")]
    [InlineData("blocked")]
    public void The_request_rejects_anything_else_as_to(string? to)
        => Assert.Contains(new ChangeStatusRequestValidator().Validate(new ChangeStatusRequest(to)).Errors, e => e.PropertyName == "to");
}
