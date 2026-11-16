using Trekky.Modules.WorkItems.Application.WorkItems;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>US-07 AC1: If-Match accepts the current ETag in the forms RFC 9110 allows.</summary>
public sealed class IfMatchTests
{
    [Theory]
    [InlineData("\"7\"")]
    [InlineData("W/\"7\"")]
    [InlineData("\"3\", \"7\"")]
    [InlineData("*")]
    public void Accepts_the_current_version(string ifMatch)
        => Assert.True(VersionedWorkItem.IfMatchAccepts(ifMatch, 7));

    [Theory]
    [InlineData("\"6\"")]
    [InlineData("7")]
    [InlineData("\"7")]
    [InlineData("")]
    [InlineData("\"3\", \"4\"")]
    public void Rejects_anything_else(string ifMatch)
        => Assert.False(VersionedWorkItem.IfMatchAccepts(ifMatch, 7));
}
