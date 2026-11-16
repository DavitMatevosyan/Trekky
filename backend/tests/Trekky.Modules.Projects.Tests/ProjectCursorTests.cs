using Trekky.Modules.Projects.Application.Projects;

namespace Trekky.Modules.Projects.Tests;

public sealed class ProjectCursorTests
{
    [Fact]
    public void Round_trips_name_and_id()
    {
        var cursor = new ProjectCursor("Mobile & Web ✓", Guid.CreateVersion7());

        Assert.True(ProjectCursor.TryDecode(cursor.Encode(), out var decoded));
        Assert.Equal(cursor, decoded);
    }

    [Fact]
    public void Is_url_safe()
        => Assert.Matches("^[A-Za-z0-9_-]+$", new ProjectCursor("a/b+c?d", Guid.CreateVersion7()).Encode());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_cursor_means_the_first_page(string? value)
    {
        Assert.True(ProjectCursor.TryDecode(value, out var cursor));
        Assert.Null(cursor);
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("e30")] // {}
    [InlineData("!!!")]
    public void Rejects_anything_it_did_not_issue(string value)
        => Assert.False(ProjectCursor.TryDecode(value, out _));
}
