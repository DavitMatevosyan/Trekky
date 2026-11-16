using Trekky.Modules.Activity.Application.History;

namespace Trekky.Modules.Activity.Tests;

public sealed class HistoryCursorTests
{
    [Fact]
    public void An_encoded_cursor_decodes_to_the_same_event_id()
    {
        var cursor = new HistoryCursor(Guid.CreateVersion7());

        Assert.True(HistoryCursor.TryDecode(cursor.Encode(), out var decoded));
        Assert.Equal(cursor, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_cursor_means_the_first_page(string? value)
    {
        Assert.True(HistoryCursor.TryDecode(value, out var decoded));
        Assert.Null(decoded);
    }

    [Theory]
    [InlineData("not a cursor")]
    [InlineData("AAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")] // 16 zero bytes: the empty id is never an event
    [InlineData("AQIDBAUGBwgJCgsMDQ4PEBES")] // 18 bytes
    public void A_malformed_cursor_is_rejected(string value)
    {
        Assert.False(HistoryCursor.TryDecode(value, out var decoded));
        Assert.Null(decoded);
    }
}
