using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Public compatibility pins, never an authentication decision or private world-state hash.</summary>
public sealed record PlayerWorldCompatibility
{
    public PlayerWorldCompatibility(EntityId worldId, EntityId branchId, ushort profileVersion,
        ContentHash admissionVersion, ContentHash packagesVersion)
    {
        if (profileVersion == 0) throw new ArgumentOutOfRangeException(nameof(profileVersion));
        ArgumentNullException.ThrowIfNull(admissionVersion);
        ArgumentNullException.ThrowIfNull(packagesVersion);
        WorldId = worldId;
        BranchId = branchId;
        ProfileVersion = profileVersion;
        AdmissionVersion = admissionVersion;
        PackagesVersion = packagesVersion;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ushort ProfileVersion { get; }
    public ContentHash AdmissionVersion { get; }
    public ContentHash PackagesVersion { get; }

    public static ContentHash ResolvePackages(IEnumerable<PackageManifest> manifests)
    {
        var resolved = PackageResolver.Resolve(manifests);
        // A manifest's version already binds its namespace, payload and complete dependency pins.
        byte[] bytes = new byte[8 + resolved.Count * 32];
        "OFPP\0\x01"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), checked((ushort)resolved.Count));
        for (int index = 0; index < resolved.Count; index++)
            resolved[index].Version.ToByteArray().CopyTo(bytes.AsSpan(8 + index * 32));
        return ContentHash.Compute(bytes);
    }

    internal bool Matches(WorldSnapshot state) => WorldId == state.WorldId && BranchId == state.BranchId
        && ProfileVersion == state.ProfileVersion && AdmissionVersion == state.AdmissionVersion;
}
