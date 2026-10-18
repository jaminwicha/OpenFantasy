using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

internal enum WorldStateEncoding { Complete, SeparatedTerrain, TypedOwners }

/// <summary>Explicit storage representation of an already admitted candidate, separate from gameplay interpretation.</summary>
internal sealed record WorldStateImage(byte[] Bytes, ConstructionOwnerDirectory? Owners = null)
{
    internal static WorldStateEncoding EncodingFor(int schema) => schema switch
    {
        WorldDatabaseSchemaThirteen.Version => WorldStateEncoding.SeparatedTerrain,
        WorldDatabaseSchemaFourteen.Version => WorldStateEncoding.TypedOwners,
        _ => WorldStateEncoding.Complete
    };

    internal static WorldStateImage Capture(WorldSnapshot world, WorldStateEncoding encoding, byte[]? complete = null) => encoding switch
    {
        WorldStateEncoding.Complete => new(complete ?? WorldSnapshotCodec.Encode(world)),
        WorldStateEncoding.SeparatedTerrain => new(ConstructionLocalityEncoding.Encode(world)),
        WorldStateEncoding.TypedOwners => FromOwners(ConstructionOwnerDirectory.Capture(world)),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding))
    };

    internal static WorldStateImage Capture(ConstructionLocalityState locality, WorldStateEncoding encoding) => encoding switch
    {
        WorldStateEncoding.SeparatedTerrain => new(ConstructionLocalityEncoding.Encode(locality)),
        WorldStateEncoding.TypedOwners => FromOwners(ConstructionOwnerDirectory.Capture(locality)),
        _ => throw new InvalidOperationException("A detached locality requires its explicit storage encoding.")
    };

    private static WorldStateImage FromOwners(ConstructionOwnerDirectory owners) => new(owners.CopyBytes(), owners);
}
