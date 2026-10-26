using Trekky.Persistence.Rls;

namespace Trekky.Persistence.Tests;

public sealed class UserRowLevelSecurityTests
{
    [Fact]
    public void Self_read_policy_is_select_only_and_compares_the_user_column_with_the_session_user()
    {
        var sql = UserRowLevelSecurity.EnableSelfReadSql("identity", "memberships");

        Assert.Contains("CREATE POLICY user_self_read ON \"identity\".\"memberships\"", sql, StringComparison.Ordinal);
        Assert.Contains("FOR SELECT", sql, StringComparison.Ordinal);
        Assert.Contains("USING (\"user_id\" = NULLIF(current_setting('app.user_id', true), '')::uuid)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("WITH CHECK", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Disable_sql_drops_only_the_self_read_policy()
    {
        var sql = UserRowLevelSecurity.DisableSelfReadSql("identity", "memberships");

        Assert.Equal("DROP POLICY IF EXISTS user_self_read ON \"identity\".\"memberships\";", sql);
    }

    [Theory]
    [InlineData("Memberships")]
    [InlineData("memberships; DROP TABLE users")]
    public void Rejects_names_that_are_not_plain_snake_case(string table)
    {
        Assert.Throws<ArgumentException>(() => UserRowLevelSecurity.EnableSelfReadSql("identity", table));
    }
}
