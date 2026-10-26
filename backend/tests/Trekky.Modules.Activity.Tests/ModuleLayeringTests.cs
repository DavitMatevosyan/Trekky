using System.Reflection;
using Trekky.Modules.Activity.Api;
using Trekky.Modules.Activity.Application.ActivityEvents;
using Trekky.Modules.Activity.Domain;
using Trekky.Modules.Activity.Infrastructure;
using Trekky.Modules.Activity.Persistence;

namespace Trekky.Modules.Activity.Tests;

/// <summary>
/// ADR-0009's dependency direction. Project references are transitive, so the compiler alone would let Api use the
/// DbContext; a compiled assembly lists only the assemblies its code actually uses, which is what these tests check.
/// </summary>
public sealed class ModuleLayeringTests
{
    private static readonly Assembly Api = typeof(ActivityModule).Assembly;
    private static readonly Assembly Application = typeof(ActivityEventMapper).Assembly;
    private static readonly Assembly Domain = typeof(ActivityEvent).Assembly;
    private static readonly Assembly Infrastructure = typeof(ActivityEventSink).Assembly;
    private static readonly Assembly Persistence = typeof(ActivityDbContext).Assembly;

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
