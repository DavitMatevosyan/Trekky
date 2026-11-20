using FluentValidation.Results;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Application.Invites;

namespace Trekky.Modules.Identity.Tests;

public sealed class InviteRequestValidatorTests
{
    private readonly CreateInviteRequestValidator _create = new();
    private readonly AcceptInviteRequestValidator _accept = new();

    [Theory]
    [InlineData("  ada@example.com ", "admin")]
    [InlineData("ada@example.com", "member")]
    [InlineData("ada@example.com", "viewer")]
    public void A_valid_invite_passes(string email, string role)
        => Assert.True(_create.Validate(new CreateInviteRequest(email, role)).IsValid);

    [Fact]
    public void Missing_fields_are_reported_by_their_json_names()
        => Assert.Equal(["email", "role"], Fields(_create.Validate(new CreateInviteRequest(null, null))));

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Ada <ada@example.com>")]
    public void An_invalid_email_is_rejected(string email)
        => Assert.Equal(["email"], Fields(_create.Validate(new CreateInviteRequest(email, "member"))));

    [Theory]
    [InlineData("owner")]
    [InlineData("Admin")]
    [InlineData("")]
    public void Only_admin_member_and_viewer_can_be_invited(string role)
        => Assert.Equal(["role"], Fields(_create.Validate(new CreateInviteRequest("ada@example.com", role))));

    [Fact]
    public void Accepting_needs_the_token_and_a_sign_up_strength_password()
    {
        Assert.True(_accept.Validate(new AcceptInviteRequest("token", "correct horse battery")).IsValid);
        Assert.Equal(["password", "token"], Fields(_accept.Validate(new AcceptInviteRequest(null, "short"))));
        Assert.Equal(["password"], Fields(_accept.Validate(new AcceptInviteRequest("token", new string('x', 129)))));
        Assert.Equal(["displayName"], Fields(_accept.Validate(new AcceptInviteRequest("token", "correct horse battery", new string('n', 81)))));
    }

    private static List<string> Fields(ValidationResult result)
        => result.Errors.Select(e => e.PropertyName).Distinct().Order(StringComparer.Ordinal).ToList();
}
