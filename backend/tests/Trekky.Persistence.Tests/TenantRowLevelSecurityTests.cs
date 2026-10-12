using Trekky.Persistence.Rls;

namespace Trekky.Persistence.Tests;

public sealed class TenantRowLevelSecurityTests
{
    [Fact]
    public void Enable_sql_forces_rls_and_compares_org_id_with_the_session_tenant()
    {
        var sql = TenantRowLevelSecurity.EnableSql("projects", "projects");

        Assert.Contains("ALTER TABLE \"projects\".\"projects\" ENABLE ROW LEVEL SECURITY;", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE \"projects\".\"projects\" FORCE ROW LEVEL SECURITY;", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE POLICY tenant_isolation ON \"projects\".\"projects\"", sql, StringComparison.Ordinal);
        Assert.Contains("USING (\"org_id\" = NULLIF(current_setting('app.org_id', true), '')::uuid)", sql, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK (\"org_id\" = NULLIF(current_setting('app.org_id', true), '')::uuid)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Enable_sql_can_use_another_column_for_the_organizations_table()
    {
        var sql = TenantRowLevelSecurity.EnableSql("identity", "organizations", orgIdColumn: "id");

        Assert.Contains("USING (\"id\" = NULLIF(", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Disable_sql_drops_the_policy_and_turns_rls_off()
    {
        var sql = TenantRowLevelSecurity.DisableSql("projects", "projects");

        Assert.Contains("DROP POLICY IF EXISTS tenant_isolation ON \"projects\".\"projects\";", sql, StringComparison.Ordinal);
        Assert.Contains("DISABLE ROW LEVEL SECURITY;", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Projects")]
    [InlineData("work-items")]
    [InlineData("1table")]
    [InlineData("notes; DROP TABLE users")]
    [InlineData("notes\"")]
    public void Rejects_names_that_are_not_plain_snake_case(string table)
    {
        Assert.Throws<ArgumentException>(() => TenantRowLevelSecurity.EnableSql("projects", table));
    }
}
