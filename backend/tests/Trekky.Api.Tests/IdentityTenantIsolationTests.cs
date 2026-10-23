using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Trekky.Api.Tests;

/// <summary>US-03 for the identity schema: organizations and memberships are isolated by RLS.</summary>
[Collection(ApiCollection.Name)]
public sealed class IdentityTenantIsolationTests(TrekkyApiFactory factory)
{
    [Fact]
    public async Task Each_user_sees_only_their_own_organization()
    {
        using var userA = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var userB = await AuthFlow.SignedUpUserAsync(factory, "Org B");

        var meA = await GetMeAsync(userA.Client, userA.Tokens.AccessToken);
        var meB = await GetMeAsync(userB.Client, userB.Tokens.AccessToken);

        Assert.Equal("Org A", meA.Organization.Name);
        Assert.Equal("Org B", meB.Organization.Name);
        Assert.NotEqual(meA.Organization.Id, meB.Organization.Id);
    }

    [Fact]
    public async Task App_role_cannot_read_another_orgs_organization_or_memberships()
    {
        using var userA = await AuthFlow.SignedUpUserAsync(factory, "Org A");
        using var userB = await AuthFlow.SignedUpUserAsync(factory, "Org B");
        var meA = await GetMeAsync(userA.Client, userA.Tokens.AccessToken);
        var meB = await GetMeAsync(userB.Client, userB.Tokens.AccessToken);

        await using var connection = new NpgsqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();

        // As org A: B's rows are invisible, A's are visible.
        await SetAsync(connection, "app.org_id", meA.Organization.Id);
        Assert.Equal(0, await CountAsync(connection, "identity.organizations", "id", meB.Organization.Id));
        Assert.Equal(0, await CountAsync(connection, "identity.memberships", "org_id", meB.Organization.Id));
        Assert.Equal(1, await CountAsync(connection, "identity.organizations", "id", meA.Organization.Id));

        // user_self_read: user B may read their own membership, but not B's organization row.
        await SetAsync(connection, "app.user_id", meB.User.Id);
        Assert.Equal(1, await CountAsync(connection, "identity.memberships", "org_id", meB.Organization.Id));
        Assert.Equal(0, await CountAsync(connection, "identity.organizations", "id", meB.Organization.Id));

        // No tenant and no user: nothing.
        await SetAsync(connection, "app.org_id", null);
        await SetAsync(connection, "app.user_id", null);
        Assert.Equal(0, await CountAsync(connection, "identity.memberships", "org_id", meA.Organization.Id));
    }

    [Fact]
    public async Task User_self_read_does_not_allow_writes()
    {
        using var user = await AuthFlow.SignedUpUserAsync(factory, "Org C");
        var me = await GetMeAsync(user.Client, user.Tokens.AccessToken);

        await using var connection = new NpgsqlConnection(factory.AppConnectionString);
        await connection.OpenAsync();
        await SetAsync(connection, "app.user_id", me.User.Id);

        await using var update = new NpgsqlCommand(
            "UPDATE identity.memberships SET role = 'viewer' WHERE user_id = @user", connection);
        update.Parameters.AddWithValue("user", me.User.Id);
        Assert.Equal(0, await update.ExecuteNonQueryAsync());
    }

    private static async Task SetAsync(NpgsqlConnection connection, string setting, Guid? value)
    {
        await using var command = new NpgsqlCommand("SELECT set_config(@setting, @value, false)", connection);
        command.Parameters.AddWithValue("setting", setting);
        command.Parameters.AddWithValue("value", value?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table, string column, Guid value)
    {
        // Table and column are constants from this file, not user input.
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE {column} = @value", connection);
        command.Parameters.AddWithValue("value", value);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<MeBody> GetMeAsync(HttpClient client, string accessToken)
    {
        using var request = AuthFlow.WithBearer(HttpMethod.Get, "/api/v1/me", accessToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MeBody>())!;
    }
}
