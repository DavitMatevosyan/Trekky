using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Tests;

public sealed class WorkItemKeyTests
{
    [Fact]
    public void Formats_project_key_and_number()
        => Assert.Equal("TRK-42", WorkItemKey.Format("TRK", 42));

    [Theory]
    [InlineData("TRK-1", "TRK", 1)]
    [InlineData("TRK-42", "TRK", 42)]
    [InlineData("A2-2147483647", "A2", int.MaxValue)]
    public void Parses_a_valid_key(string key, string projectKey, int number)
    {
        Assert.True(WorkItemKey.TryParse(key, out var parsedProject, out var parsedNumber));
        Assert.Equal(projectKey, parsedProject);
        Assert.Equal(number, parsedNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("TRK")]
    [InlineData("TRK-")]
    [InlineData("TRK-0")]
    [InlineData("TRK-01")]
    [InlineData("trk-1")]
    [InlineData("T-1")]
    [InlineData("TRK-1\n")]
    [InlineData("TRK-2147483648")]
    [InlineData("TRK--1")]
    [InlineData("TRK-1-2")]
    public void Rejects_anything_else(string? key)
        => Assert.False(WorkItemKey.TryParse(key, out _, out _));
}
