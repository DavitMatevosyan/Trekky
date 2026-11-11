using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>US-11 AC4: a card's rank always lands between its neighbours, however often cards move.</summary>
public sealed class BoardRankTests
{
    [Fact]
    public void The_first_card_of_an_empty_column_gets_the_first_rank()
        => Assert.Equal(BoardRank.First, BoardRank.Between(null, null));

    [Theory]
    [InlineData(null, "a0", "Zz")]
    [InlineData("a0", null, "a1")]
    [InlineData("a0", "a1", "a0V")]
    [InlineData("a0V", "a1", "a0l")]
    [InlineData("Zz", "a0", "ZzV")]
    [InlineData("az", null, "b00")]
    [InlineData(null, "b00", "az")]
    [InlineData("a0", "a0V", "a0G")]
    [InlineData("c001", "c002", "c001V")]
    public void A_rank_between_neighbours_is_the_expected_one(string? before, string? after, string expected)
        => Assert.Equal(expected, BoardRank.Between(before, after));

    [Fact]
    public void Ranks_keep_their_order_through_many_random_moves()
    {
        var random = new Random(17);
        var column = new List<string>();
        for (var i = 0; i < 2_000; i++)
        {
            var index = random.Next(column.Count + 1);
            var before = index == 0 ? null : column[index - 1];
            var after = index == column.Count ? null : column[index];
            var rank = BoardRank.Between(before, after);

            Assert.True(BoardRank.IsValid(rank), rank);
            Assert.True(before is null || string.CompareOrdinal(before, rank) < 0, $"{before} < {rank}");
            Assert.True(after is null || string.CompareOrdinal(rank, after) < 0, $"{rank} < {after}");
            column.Insert(index, rank);
        }
    }

    [Fact]
    public void Cards_that_keep_going_to_the_top_or_bottom_keep_short_ranks()
    {
        string? top = null;
        string? bottom = null;
        for (var i = 0; i < 10_000; i++)
        {
            top = BoardRank.Between(null, top);
            bottom = BoardRank.Between(bottom, null);
        }

        Assert.True(top!.Length <= 5, top);
        Assert.True(bottom!.Length <= 5, bottom);
    }

    [Fact]
    public void Cards_that_keep_going_between_the_same_two_grow_slowly()
    {
        var low = "a0";
        var high = "a1";
        for (var i = 0; i < 100; i++)
        {
            high = BoardRank.Between(low, high);
        }

        // About one character per five or six halvings.
        Assert.True(high.Length < 25, high);
    }

    [Theory]
    [InlineData("a1", "a0")]
    [InlineData("a0", "a0")]
    public void Neighbours_out_of_order_are_rejected(string before, string after)
        => Assert.Throws<ArgumentException>(() => BoardRank.Between(before, after));

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("a00")]
    [InlineData("a0-")]
    [InlineData("!0")]
    [InlineData("A00000000000000000000000000")]
    public void Malformed_ranks_are_not_valid(string rank)
    {
        Assert.False(BoardRank.IsValid(rank));
        Assert.Throws<ArgumentException>(() => BoardRank.Between(rank, null));
    }
}
