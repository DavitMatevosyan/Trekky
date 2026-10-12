using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Trekky.Persistence.Tenancy;

namespace Trekky.Persistence.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public void Has_no_tenant_until_set()
    {
        Assert.Null(new TenantContext().OrgId);
    }

    [Fact]
    public void Setting_the_same_org_twice_is_allowed()
    {
        var orgId = Guid.CreateVersion7();
        var tenant = new TenantContext();

        tenant.Set(orgId);
        tenant.Set(orgId);

        Assert.Equal(orgId, tenant.OrgId);
    }

    [Fact]
    public void Switching_to_another_org_in_the_same_scope_throws()
    {
        var tenant = new TenantContext();
        tenant.Set(Guid.CreateVersion7());

        Assert.Throws<InvalidOperationException>(() => tenant.Set(Guid.CreateVersion7()));
    }

    [Fact]
    public void Empty_org_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new TenantContext().Set(Guid.Empty));
    }

    [Fact]
    public async Task Middleware_sets_the_tenant_from_the_org_id_claim()
    {
        var orgId = Guid.CreateVersion7();
        var tenant = await RunMiddlewareAsync(new Claim("org_id", orgId.ToString()));

        Assert.Equal(orgId, tenant.OrgId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Middleware_leaves_no_tenant_without_a_valid_org_id_claim(string? claimValue)
    {
        var tenant = claimValue is null
            ? await RunMiddlewareAsync()
            : await RunMiddlewareAsync(new Claim("org_id", claimValue));

        Assert.Null(tenant.OrgId);
    }

    private static async Task<TenantContext> RunMiddlewareAsync(params Claim[] claims)
    {
        var tenant = new TenantContext();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test")),
        };
        var nextCalled = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, tenant);

        Assert.True(nextCalled);
        return tenant;
    }
}
