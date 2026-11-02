using Trekky.Modules.WorkItems.Application.WorkItems;

namespace Trekky.Modules.WorkItems.Tests;

public sealed class WorkItemCursorTests
{
    [Fact]
    public void Round_trips_update_time_and_id()
    {
        var cursor = new WorkItemCursor(new DateTimeOffset(2026, 11, 2, 9, 41, 12, TimeSpan.Zero).AddTicks(1230), Guid.CreateVersion7());

        Assert.True(WorkItemCursor.TryDecode(cursor.Encode(), out var decoded));
        Assert.Equal(cursor, decoded);
    }

    [Fact]
    public void A_time_with_an_offset_is_read_as_utc()
    {
        var cursor = new WorkItemCursor(new DateTimeOffset(2026, 11, 2, 11, 0, 0, TimeSpan.FromHours(2)), Guid.CreateVersion7());

        Assert.True(WorkItemCursor.TryDecode(cursor.Encode(), out var decoded));
        Assert.Equal(TimeSpan.Zero, decoded!.UpdatedAt.Offset);
        Assert.Equal(cursor.UpdatedAt, decoded.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_cursor_means_the_first_page(string? value)
    {
        Assert.True(WorkItemCursor.TryDecode(value, out var cursor));
        Assert.Null(cursor);
    }

    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("e30")] // {}
    [InlineData("eyJJIjoiMDE5MjdmM2EtNmMxZS03YjRhLTlkMmUtM2Y1YThjN2IxZTIwIn0")] // {"I":"…"} without a time
    public void Rejects_a_cursor_it_did_not_make(string value)
        => Assert.False(WorkItemCursor.TryDecode(value, out _));
}
