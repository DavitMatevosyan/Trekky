using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Trekky.Persistence.Tests;

/// <summary>
/// US-03 / ADR-0002: one organization can never read or change another organization's rows,
/// nothing is visible without a tenant, and pooled connections do not leak a tenant.
/// Runs against real Postgres as the trekky_app role, through the same DbContext wiring modules use.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantIsolationTests : IDisposable
{
    private readonly Guid _orgA = Guid.CreateVersion7();
    private readonly Guid _orgB = Guid.CreateVersion7();
    private readonly PostgresFixture _postgres;
    private readonly TenantScopes _scopes;

    public TenantIsolationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _scopes = new TenantScopes(postgres.AppConnectionString);
    }

    public void Dispose() => _scopes.Dispose();

    [Fact]
    public async Task Each_org_sees_only_its_own_rows()
    {
        var noteA = await InsertAsync(_scopes, _orgA, "A's note");
        var noteB = await InsertAsync(_scopes, _orgB, "B's note");

        await using (var a = _scopes.For(_orgA))
        {
            var visible = await a.Db.Notes.ToListAsync();
            Assert.Contains(visible, n => n.Id == noteA);
            Assert.DoesNotContain(visible, n => n.Id == noteB);
            Assert.All(visible, n => Assert.Equal(_orgA, n.OrgId));
        }

        await using (var b = _scopes.For(_orgB))
        {
            var visible = await b.Db.Notes.ToListAsync();
            Assert.Contains(visible, n => n.Id == noteB);
            Assert.All(visible, n => Assert.Equal(_orgB, n.OrgId));
        }
    }

    [Fact]
    public async Task Org_cannot_update_or_delete_another_orgs_rows()
    {
        var noteB = await InsertAsync(_scopes, _orgB, "original");

        await using (var a = _scopes.For(_orgA))
        {
            var updated = await a.Db.Database.ExecuteSqlAsync(
                $"UPDATE tenancy_test.notes SET text = 'changed by A' WHERE id = {noteB}");
            var deleted = await a.Db.Database.ExecuteSqlAsync(
                $"DELETE FROM tenancy_test.notes WHERE id = {noteB}");

            Assert.Equal(0, updated);
            Assert.Equal(0, deleted);
        }

        await using var b = _scopes.For(_orgB);
        var note = await b.Db.Notes.SingleAsync(n => n.Id == noteB);
        Assert.Equal("original", note.Text);
    }

    [Fact]
    public async Task Org_cannot_insert_rows_for_another_org()
    {
        await using var a = _scopes.For(_orgA);
        a.Db.Notes.Add(new Note { OrgId = _orgB, Text = "planted by A" });

        await AssertRowLevelSecurityViolationAsync(() => a.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Without_a_tenant_no_rows_are_visible_and_inserts_fail()
    {
        await InsertAsync(_scopes, _orgA, "exists");

        await using var none = _scopes.For(orgId: null);

        Assert.Empty(await none.Db.Notes.ToListAsync());

        none.Db.Notes.Add(new Note { OrgId = _orgA, Text = "no tenant" });
        await AssertRowLevelSecurityViolationAsync(() => none.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Reused_pooled_connection_does_not_keep_the_previous_tenant()
    {
        // One connection in the pool, so every scope below gets the same physical connection.
        var singleConnection = new NpgsqlConnectionStringBuilder(_postgres.AppConnectionString)
        {
            MaxPoolSize = 1,
            ApplicationName = "trekky-pool-reuse-test",
        }.ConnectionString;
        using var scopes = new TenantScopes(singleConnection);

        var noteA = await InsertAsync(scopes, _orgA, "A's note");

        int pidA;
        await using (var a = scopes.For(_orgA))
        {
            pidA = await BackendPidAsync(a.Db);
            Assert.Contains(await a.Db.Notes.ToListAsync(), n => n.Id == noteA);
        }

        await using (var none = scopes.For(orgId: null))
        {
            Assert.Equal(pidA, await BackendPidAsync(none.Db));
            Assert.Empty(await none.Db.Notes.ToListAsync());
        }

        await using (var b = scopes.For(_orgB))
        {
            Assert.Equal(pidA, await BackendPidAsync(b.Db));
            Assert.DoesNotContain(await b.Db.Notes.ToListAsync(), n => n.Id == noteA);
        }
    }

    [Fact]
    public async Task App_role_is_not_a_superuser_and_cannot_bypass_rls()
    {
        await using var a = _scopes.For(_orgA);

        var isSuperuser = await a.Db.Database
            .SqlQuery<bool>($"SELECT rolsuper AS \"Value\" FROM pg_roles WHERE rolname = current_user")
            .SingleAsync();
        var bypassesRls = await a.Db.Database
            .SqlQuery<bool>($"SELECT rolbypassrls AS \"Value\" FROM pg_roles WHERE rolname = current_user")
            .SingleAsync();

        Assert.False(isSuperuser);
        Assert.False(bypassesRls);
    }

    private static async Task<Guid> InsertAsync(TenantScopes scopes, Guid orgId, string text)
    {
        await using var scope = scopes.For(orgId);
        var note = new Note { OrgId = orgId, Text = text };
        scope.Db.Notes.Add(note);
        await scope.Db.SaveChangesAsync();
        return note.Id;
    }

    private static Task<int> BackendPidAsync(NotesDbContext db)
        => db.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync();

    private static async Task AssertRowLevelSecurityViolationAsync(Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, postgres.SqlState);
    }
}
