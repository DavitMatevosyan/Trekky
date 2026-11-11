using Trekky.Modules.WorkItems.Application.Comments;
using Trekky.Modules.WorkItems.Application.Contracts;
using Trekky.Modules.WorkItems.Domain;

namespace Trekky.Modules.WorkItems.Tests;

/// <summary>US-12: what a comment may contain, and the excerpt its event carries.</summary>
public sealed class CommentRulesTests
{
    private readonly AddCommentRequestValidator validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\t ")]
    public void A_comment_with_nothing_to_say_is_rejected(string? body)
        => Assert.Equal(
            [AddCommentRequestValidator.BodyRequiredMessage],
            validator.Validate(new AddCommentRequest(body)).Errors.Select(e => e.ErrorMessage));

    [Fact]
    public void A_comment_with_a_NUL_character_is_rejected()
        => Assert.Equal(
            [AddCommentRequestValidator.NullCharacterMessage],
            validator.Validate(new AddCommentRequest("hi\0there")).Errors.Select(e => e.ErrorMessage));

    [Fact]
    public void A_comment_up_to_20000_characters_is_accepted()
    {
        Assert.True(validator.Validate(new AddCommentRequest("Looks good")).IsValid);
        Assert.True(validator.Validate(new AddCommentRequest(new string('x', 20_000))).IsValid);
        Assert.Equal(
            "bodyMd",
            Assert.Single(validator.Validate(new AddCommentRequest(new string('x', 20_001))).Errors).PropertyName);
    }

    [Fact]
    public void The_excerpt_is_the_first_200_characters()
    {
        Assert.Equal("Short", WorkItemComment.Excerpt("Short"));
        Assert.Equal(new string('a', 200), WorkItemComment.Excerpt(new string('a', 250)));
    }

    [Fact]
    public void The_excerpt_never_cuts_a_character_in_two()
        => Assert.Equal(new string('a', 199), WorkItemComment.Excerpt(new string('a', 199) + "😀" + "tail"));
}
