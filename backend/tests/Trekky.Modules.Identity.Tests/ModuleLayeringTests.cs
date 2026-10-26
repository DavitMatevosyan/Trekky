using System.Reflection;
using Trekky.Modules.Identity.Api;
using Trekky.Modules.Identity.Application.Contracts;
using Trekky.Modules.Identity.Domain;
using Trekky.Modules.Identity.Infrastructure;
using Trekky.Modules.Identity.Persistence;

namespace Trekky.Modules.Identity.Tests;

/// <summary>
/// ADR-0009's dependency direction. Project references are transitive, so the compiler alone would let Api use the
/// DbContext; a compiled assembly lists only the assemblies its code actually uses, which is what these tests check.
/// </summary>
public sealed class ModuleLayeringTests
{
    private static readonly Assembly Api = typeof(IdentityModule).Assembly;
    private static readonly Assembly Application = typeof(SignUpRequest).Assembly;
    private static readonly Assembly Domain = typeof(User).Assembly;
    private static readonly Assembly Infrastructure = typeof(AccountService).Assembly;
    private static readonly Assembly Persistence = typeof(IdentityDbContext).Assembly;

    public static TheoryData<string, string> ForbiddenReferences => new()
    {
        { nameof(Domain), nameof(Application) },
        { nameof(Domain), nameof(Persistence) },
        { nameof(Domain), nameof(Infrastructure) },
        { nameof(Domain), nameof(Api) },
        { nameof(Application), nameof(Persistence) },
        { nameof(Application), nameof(Infrastructure) },
        { nameof(Application), nameof(Api) },
        { nameof(Persistence), nameof(Application) },
        { nameof(Persistence), nameof(Infrastructure) },
        { nameof(Persistence), nameof(Api) },
        { nameof(Infrastructure), nameof(Api) },
        { nameof(Api), nameof(Persistence) },
    };

    [Theory]
    [MemberData(nameof(ForbiddenReferences))]
    public void Layer_does_not_use_a_layer_it_must_not_depend_on(string layer, string forbidden)
        => Assert.DoesNotContain(Layer(forbidden).GetName().Name, ReferencedNames(Layer(layer)));

    [Fact]
    public void The_check_sees_real_references()
    {
        Assert.Contains(Persistence.GetName().Name, ReferencedNames(Infrastructure));
        Assert.Contains(Infrastructure.GetName().Name, ReferencedNames(Api));
    }

    private static Assembly Layer(string name) => name switch
    {
        nameof(Api) => Api,
        nameof(Application) => Application,
        nameof(Domain) => Domain,
        nameof(Infrastructure) => Infrastructure,
        nameof(Persistence) => Persistence,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static IEnumerable<string?> ReferencedNames(Assembly assembly)
        => assembly.GetReferencedAssemblies().Select(reference => reference.Name);
}
