using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>US-01 sign-up and US-02 login against the real API and Postgres.</summary>
[Collection(ApiCollection.Name)]
public sealed class SignUpAndLoginTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Sign_up_creates_the_organization_with_the_user_as_owner()
    {
        using var client = factory.CreateBrowserClient();
        var email = AuthFlow.NewEmail();

        var response = await AuthFlow.SignUpAsync(client, email, "Analytical Engines");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeBody>();
        Assert.NotNull(me);
        Assert.Equal("owner", me.Role);
        Assert.Equal("Analytical Engines", me.Organization.Name);
        Assert.Equal(email, me.User.Email);
        Assert.Equal(email[..email.IndexOf('@', StringComparison.Ordinal)], me.User.DisplayName);
        Assert.Equal(7, me.User.Id.Version);
        Assert.Equal(7, me.Organization.Id.Version);
        AssertSessionCookie(response);
    }

    [Fact]
    public async Task Sign_up_with_a_registered_email_returns_409_whatever_the_case()
    {
        using var client = factory.CreateBrowserClient();
        var email = AuthFlow.NewEmail();
        Assert.Equal(HttpStatusCode.Created, (await AuthFlow.SignUpAsync(client, email)).StatusCode);

        using var other = factory.CreateBrowserClient();
        var response = await AuthFlow.SignUpAsync(other, email.ToUpperInvariant(), "Another org");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.Equal("An account with this email already exists.", problem?.Detail);
        Assert.False(response.Headers.Contains("Set-Cookie"));

        // US-01 AC5: the failed sign-up left no organization behind.
        await using var connection = new NpgsqlConnection(factory.SuperuserConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM identity.organizations WHERE name = 'Another org'", connection);
        Assert.Equal(0L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Sign_up_with_invalid_input_returns_a_validation_problem()
    {
        using var client = factory.CreateBrowserClient();

        var response = await AuthFlow.SignUpAsync(client, "not-an-email", organizationName: "", password: "too short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
        Assert.NotNull(problem?.Errors);
        Assert.Equal(new[] { "email", "organizationName", "password" }, problem.Errors.Keys.Order());
    }

    [Fact]
    public async Task Login_with_the_right_password_sets_the_session_cookie()
    {
        var email = AuthFlow.NewEmail();
        using (var signUpClient = factory.CreateBrowserClient())
        {
            await AuthFlow.SignUpAsync(signUpClient, email);
        }

        using var client = factory.CreateBrowserClient();
        var response = await AuthFlow.LogInAsync(client, email);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertSessionCookie(response);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_generic_401()
    {
        var email = AuthFlow.NewEmail();
        using var client = factory.CreateBrowserClient();
        await AuthFlow.SignUpAsync(client, email);

        using var anonymous = factory.CreateBrowserClient();
        var wrongPassword = await AuthFlow.LogInAsync(anonymous, email, "wrong password!!");
        var unknownEmail = await AuthFlow.LogInAsync(anonymous, AuthFlow.NewEmail());

        foreach (var response in new[] { wrongPassword, unknownEmail })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>();
            Assert.Equal("Email or password is incorrect.", problem?.Detail);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account()
    {
        var email = AuthFlow.NewEmail();
        using (var signUpClient = factory.CreateBrowserClient())
        {
            await AuthFlow.SignUpAsync(signUpClient, email);
        }

        using var client = factory.CreateBrowserClient();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await AuthFlow.LogInAsync(client, email, "wrong password!!")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Locked, (await AuthFlow.LogInAsync(client, email, "wrong password!!")).StatusCode);

        // Locked even with the right password.
        var rightPassword = await AuthFlow.LogInAsync(client, email);
        Assert.Equal(HttpStatusCode.Locked, rightPassword.StatusCode);
        Assert.False(rightPassword.Headers.Contains("Set-Cookie"));
    }

    private static void AssertSessionCookie(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var session = Assert.Single(cookies, c => c.StartsWith("trekky.session=", StringComparison.Ordinal));
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", session, StringComparison.OrdinalIgnoreCase);
    }
}
