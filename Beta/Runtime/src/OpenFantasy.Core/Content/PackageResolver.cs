using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Content;

/// <summary>Produces a deterministic dependency-first plan for an entirely pinned package set.</summary>
public static class PackageResolver
{
    public const int MaximumPackages = 256;

    public static ReadOnlyCollection<PackageManifest> Resolve(IEnumerable<PackageManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);
        var packages = new Dictionary<PackageId, PackageManifest>();
        foreach (PackageManifest manifest in manifests)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            if (packages.Count == MaximumPackages)
            {
                throw new InvalidDataException("The package set exceeds its limit.");
            }
            if (!packages.TryAdd(manifest.Id, manifest))
            {
                throw new InvalidDataException("Duplicate package namespace: " + manifest.Id);
            }
        }

        var visiting = new HashSet<PackageId>();
        var visited = new HashSet<PackageId>();
        var order = new List<PackageManifest>();
        foreach (PackageManifest manifest in packages.Values.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            Visit(manifest);
        }
        return order.AsReadOnly();

        void Visit(PackageManifest manifest)
        {
            if (visited.Contains(manifest.Id))
            {
                return;
            }
            if (!visiting.Add(manifest.Id))
            {
                throw new InvalidDataException("Cyclic package dependency at: " + manifest.Id);
            }
            foreach (PackageDependency dependency in manifest.Dependencies)
            {
                if (!packages.TryGetValue(dependency.Id, out PackageManifest? target))
                {
                    throw new InvalidDataException("Missing package dependency: " + dependency.Id);
                }
                Visit(target);
                if (dependency.Version != target.Version)
                {
                    throw new InvalidDataException("Package dependency version mismatch: " + dependency.Id);
                }
            }
            visiting.Remove(manifest.Id);
            visited.Add(manifest.Id);
            order.Add(manifest);
        }
    }
}
