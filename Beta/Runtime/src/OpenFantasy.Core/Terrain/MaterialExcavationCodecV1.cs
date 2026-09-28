using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Canonical component bytes bound to the actual complete owner; not a full-world archive admission.</summary>
public static class MaterialExcavationCodecV1
{
    public const int MaximumEncodedByteLength = 32 * 1024 * 1024;
    private const int MaximumSpatialByteLength = 8 * 1024 * 1024;

    public static byte[] Encode(MaterialExcavationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Production.Continuation?.SupportsExcavation != true)
            throw new ArgumentException("Excavation material bytes require their explicitly continued owner.", nameof(state));
        byte[] bytes = CanonicalEncodingV1.Frame("excavation.material-state", CanonicalEncodingV1.Unsigned64(1),
            state.Boundary.Source.ContentHash.ToByteArray(), AuthorityEncodingV1.Encode(state.Inventory),
            ProductionCodecV8.Encode(state.Production), ExcavationMaterialSource.EncodeSpace(state.Space),
            GatheringCodecV1.Encode(state.Gathering), state.Boundary.ContentHash.ToByteArray());
        if (bytes.Length > MaximumEncodedByteLength) throw new ArgumentException("Excavation material state exceeds its finite byte bound.");
        return bytes;
    }

    public static MaterialExcavationState Decode(ReadOnlySpan<byte> bytes, ExcavationProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 8);
            reader.Tag("excavation.material-state"u8);
            reader.Version();
            if (!reader.Exact(32).SequenceEqual(dependencies.Source.ContentHash.ToByteArray()))
                throw new FormatException("Excavation decoding requires its exact source admission.");
            var inventory = AuthorityEncodingV1.Decode(reader.Field(AuthorityEncodingV1.MaximumStateByteLength));
            var production = ProductionCodecV8.Decode(reader.Field(ProductionCodecV8.MaximumSnapshotByteLength),
                dependencies.Context, dependencies.ExcavationEvidence);
            var components = WorldSpatialCodecV1.DecodeComponents(reader.Field(MaximumSpatialByteLength), dependencies.SpatialVersion);
            var space = new LocalSpatialSnapshot(components.Scope, components.Regions, components.Actors, dependencies.Source.Structures,
                dependencies.Source.GroundSupport, dependencies.Source.StructureSupport);
            var gathering = GatheringCodecV1.Decode(reader.Field(GatheringCodecV1.MaximumSnapshotByteLength));
            var boundary = production.MaterialHistory!.Excavation!;
            if (!reader.Exact(32).SequenceEqual(boundary.ContentHash.ToByteArray()))
                throw new FormatException("Excavation work, geometry and frontiers require their exact replayed boundary.");
            reader.Complete();
            var result = new MaterialExcavationState(inventory, production, gathering, space, boundary);
            if (result.ContentHash != dependencies.OwnerHash || !bytes.SequenceEqual(Encode(result)))
                throw new FormatException("Excavation bytes must reproduce the actual complete qualified owner canonically.");
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Invalid continued excavation material state: " + error.Message, error);
        }
    }
}
