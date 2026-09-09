using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Tests.Content;

public sealed class PackageResolverTests
{
    [Fact]
    public void SharedDependenciesLoadOnceBeforeDependentsAcrossInputPermutations()
    {
        PackageManifest rules = Create("game.rules");
        PackageManifest area = Create("game.area", Pin(rules));
        PackageManifest people = Create("game.people", Pin(rules));
        PackageManifest world = Create("game.world", Pin(area), Pin(people));
        var inputs = new[] { world, rules, people, area };
        Assert.Equal(new[] { "game.rules", "game.area", "game.people", "game.world" },
            PackageResolver.Resolve(inputs).Select(item => item.Id.Value));
        Assert.Equal(PackageResolver.Resolve(inputs), PackageResolver.Resolve(inputs.Reverse()));
    }

    [Fact]
    public void MissingAndMismatchedDependenciesReject()
    {
        PackageManifest rules = Create("game.rules");
        PackageManifest area = Create("game.area", Pin(rules));
        Assert.Throws<InvalidDataException>(() => PackageResolver.Resolve([area]));
        var changed = new PackageManifest(rules.Id, ContentHash.Compute("changed content"u8), []);
        Assert.Throws<InvalidDataException>(() => PackageResolver.Resolve([area, changed]));
    }

    [Fact]
    public void CyclesAndDuplicateNamespacesRejectInsteadOfUsingAnArbitraryWinner()
    {
        var anyHash = ContentHash.Compute("pin"u8);
        PackageManifest a = Create("game.a", new PackageDependency(new PackageId("game.b"), anyHash));
        PackageManifest b = Create("game.b", new PackageDependency(new PackageId("game.a"), anyHash));
        Assert.Contains("Cyclic", Assert.Throws<InvalidDataException>(() => PackageResolver.Resolve([b, a])).Message);
        Assert.Throws<InvalidDataException>(() => PackageResolver.Resolve([Create("game.a"), Create("game.a")]));
    }

    [Fact]
    public void EmptyAndBoundedPackageSetsHaveExplicitBehavior()
    {
        Assert.Empty(PackageResolver.Resolve([]));
        Assert.Throws<InvalidDataException>(() => PackageResolver.Resolve(
            Enumerable.Range(0, 257).Select(index => Create("game.p" + index))));
        Assert.Equal(256, PackageResolver.Resolve(
            Enumerable.Range(0, 256).Select(index => Create("game.p" + index))).Count);
    }

    private static PackageManifest Create(string id, params PackageDependency[] dependencies) =>
        new(new PackageId(id), ContentHash.Compute("payload"u8), dependencies);

    private static PackageDependency Pin(PackageManifest manifest) => new(manifest.Id, manifest.Version);
}
