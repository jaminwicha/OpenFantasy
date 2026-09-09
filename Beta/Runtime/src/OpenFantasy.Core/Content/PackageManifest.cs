using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Content;

/// <summary>
/// Immutable package metadata. Its version binds namespace, payload and dependency pins.
/// Passing this contract does not validate the payload's gameplay semantics or authorize execution.
/// </summary>
public sealed class PackageManifest
{
    public const int SchemaVersion = 1;
    public const int MaximumDependencies = 64;

    public PackageId Id { get; }
    public ContentHash PayloadHash { get; }
    public ContentHash Version { get; }
    public ReadOnlyCollection<PackageDependency> Dependencies { get; }

    public PackageManifest(PackageId id, ContentHash payloadHash, IEnumerable<PackageDependency> dependencies)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(payloadHash);
        ArgumentNullException.ThrowIfNull(dependencies);
        var selected = new List<PackageDependency>();
        var names = new HashSet<PackageId>();
        foreach (PackageDependency dependency in dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency);
            if (selected.Count == MaximumDependencies)
            {
                throw new ArgumentException("The manifest exceeds its dependency limit.", nameof(dependencies));
            }
            if (!names.Add(dependency.Id))
            {
                throw new ArgumentException("A dependency namespace must occur only once.", nameof(dependencies));
            }
            selected.Add(dependency);
        }

        selected.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id.Value, right.Id.Value));
        Id = id;
        PayloadHash = payloadHash;
        Dependencies = selected.AsReadOnly();
        Version = ContentHash.Compute(ToCanonicalBytes());
    }

    public byte[] ToCanonicalBytes()
    {
        var fields = new List<ReadOnlyMemory<byte>>
        {
            CanonicalEncodingV1.Text(Id.Value, nameof(Id)),
            PayloadHash.ToByteArray()
        };
        foreach (PackageDependency dependency in Dependencies)
        {
            fields.Add(CanonicalEncodingV1.Frame("package.dependency.v1",
                CanonicalEncodingV1.Text(dependency.Id.Value, nameof(dependency.Id)),
                dependency.Version.ToByteArray()));
        }
        return CanonicalEncodingV1.Frame("package.manifest.v1", fields.ToArray());
    }

    public void ValidatePayload(ReadOnlySpan<byte> payload)
    {
        if (ContentHash.Compute(payload) != PayloadHash)
        {
            throw new InvalidDataException("The package payload differs from its pinned hash.");
        }
    }
}
