using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Trekky.SharedKernel;

namespace Trekky.Api.Tests;

/// <summary>TREK-46: every endpoint names the role policy for its action (ADR-0026), and each policy lets in exactly its roles.</summary>
[Collection(ApiCollection.Name)]
public sealed class EndpointPolicyTests(TrekkyApiFactory factory)
{
    /// <summary>The only endpoints that answer without a signed-in member.</summary>
    private static readonly string[] AnonymousEndpoints =
    [
        "GET /api/v1/health",
        "POST /api/v1/auth/sign-up",
        "POST /api/v1/auth/login",
        "GET,POST /connect/authorize",
        "POST /connect/token",
        "GET /openapi/{documentName}.json",
    ];

    /// <summary>Endpoints with their own authentication policy instead of a role policy.</summary>
    private static readonly Dictionary<string, string> SpecialPolicies = new()
    {
        ["POST /api/v1/auth/logout"] = "BearerOrSessionCookie",
    };

    [Fact]
    public void Only_the_sign_in_endpoints_allow_anonymous_callers()
    {
        var anonymous = Endpoints()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Describe)
            .Order(StringComparer.Ordinal);

        Assert.Equal(AnonymousEndpoints.Order(StringComparer.Ordinal), anonymous);
    }

    [Fact]
    public void Every_other_endpoint_names_a_role_policy_and_every_write_needs_more_than_read()
    {
        var problems = new List<string>();
        foreach (var endpoint in Endpoints().Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null))
        {
            var name = Describe(endpoint);
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
            if (SpecialPolicies.TryGetValue(name, out var special))
            {
                if (!policies.SequenceEqual([special]))
                {
                    problems.Add($"{name}: expected {special}, has [{string.Join(", ", policies)}]");
                }

                continue;
            }

            if (policies.Count != 1 || policies[0] is not { } policy || !TrekkyPolicies.Roles.ContainsKey(policy))
            {
                problems.Add($"{name}: needs exactly one TrekkyPolicies policy, has [{string.Join(", ", policies)}]");
                continue;
            }

            // An endpoint for every method (Map, MapFallback) can change data too.
            var methods = Methods(endpoint);
            var reads = methods.Count > 0 && methods.All(m => m == HttpMethods.Get);
            if (!reads && policy == TrekkyPolicies.ReadWork)
            {
                problems.Add($"{name}: changes data but only needs {policy}");
            }
        }

        Assert.Empty(problems);
    }

    public static TheoryData<string, string, bool> Matrix => new()
    {
        { TrekkyPolicies.ReadWork, TrekkyRoles.Viewer, true },
        { TrekkyPolicies.ReadWork, TrekkyRoles.Member, true },
        { TrekkyPolicies.ReadWork, TrekkyRoles.Admin, true },
        { TrekkyPolicies.ReadWork, TrekkyRoles.Owner, true },
        { TrekkyPolicies.EditWork, TrekkyRoles.Viewer, false },
        { TrekkyPolicies.EditWork, TrekkyRoles.Member, true },
        { TrekkyPolicies.EditWork, TrekkyRoles.Admin, true },
        { TrekkyPolicies.EditWork, TrekkyRoles.Owner, true },
        { TrekkyPolicies.ManageProjects, TrekkyRoles.Viewer, false },
        { TrekkyPolicies.ManageProjects, TrekkyRoles.Member, false },
        { TrekkyPolicies.ManageProjects, TrekkyRoles.Admin, true },
        { TrekkyPolicies.ManageProjects, TrekkyRoles.Owner, true },
        { TrekkyPolicies.ManageMembers, TrekkyRoles.Viewer, false },
        { TrekkyPolicies.ManageMembers, TrekkyRoles.Member, false },
        { TrekkyPolicies.ManageMembers, TrekkyRoles.Admin, true },
        { TrekkyPolicies.ManageMembers, TrekkyRoles.Owner, true },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Each_policy_lets_in_exactly_its_roles(string policy, string role, bool allowed)
    {
        var result = await AuthorizeAsync(policy, Caller(role));

        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public void Every_policy_is_in_the_matrix()
        => Assert.Equal(
            TrekkyPolicies.Roles.Keys.Order(StringComparer.Ordinal),
            Matrix.Select(row => (string)row[0]).Distinct().Order(StringComparer.Ordinal));

    [Theory]
    [InlineData(null)]
    [InlineData("superuser")]
    public async Task A_caller_without_a_known_role_passes_no_policy_not_even_the_fallback(string? role)
    {
        var caller = Caller(role);
        foreach (var policy in TrekkyPolicies.Roles.Keys)
        {
            Assert.False((await AuthorizeAsync(policy, caller)).Succeeded, policy);
        }

        var fallback = await factory.Services.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        Assert.NotNull(fallback);
        Assert.False((await factory.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(caller, null, fallback)).Succeeded);
    }

    private Task<AuthorizationResult> AuthorizeAsync(string policy, ClaimsPrincipal caller)
        => factory.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(caller, null, policy);

    private static ClaimsPrincipal Caller(string? role)
    {
        var claims = new List<Claim> { new(TrekkyClaimTypes.Subject, Guid.CreateVersion7().ToString()) };
        if (role is not null)
        {
            claims.Add(new Claim(TrekkyClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private IEnumerable<RouteEndpoint> Endpoints()
        => factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

    private static IReadOnlyList<string> Methods(Endpoint endpoint)
        => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];

    private static string Describe(RouteEndpoint endpoint)
        => $"{string.Join(",", Methods(endpoint))} /{endpoint.RoutePattern.RawText?.TrimStart('/')}";
}
