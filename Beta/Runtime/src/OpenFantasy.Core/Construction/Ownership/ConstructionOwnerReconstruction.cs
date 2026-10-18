using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.CanonicalEncodingV1;

namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>
/// Reconstructs the complete profile-five admission cohort from stored owner and terrain images.
/// A caller supplies the directory hash selected by verified history; self-consistency is not authority.
/// </summary>
public static class ConstructionOwnerReconstruction
{
    public static WorldSnapshot Decode(ReadOnlySpan<byte> directory, ContentHash expectedHash,
        Func<ContentHash, byte[]> readOwner, Func<TerrainRegionReference, TerrainRegionSnapshot> readTerrain)
    {
        ArgumentNullException.ThrowIfNull(readOwner);
        ArgumentNullException.ThrowIfNull(readTerrain);
        if (directory.Length > ConstructionOwnerDirectory.MaximumDirectoryByteLength)
            throw new FormatException("The owner directory exceeds its metadata envelope.");
        byte[] original = directory.ToArray();
        try
        {
            var owners = new StoredConstructionOwners(original, expectedHash, readOwner);
            var basis = owners.Singleton(ConstructionOwnerKind.InventoryBasis, 5);
            var timing = owners.Singleton(ConstructionOwnerKind.Timing, 3);
            var production = RestoreProduction(owners);
            var construction = RestoreConstruction(owners);
            var inventory = RestoreInventory(owners, basis);
            var actors = owners.Owners(ConstructionOwnerKind.Actor, 3).ToArray();
            foreach (var actor in actors)
                if (!actor.Fields[0].AsSpan().SequenceEqual(owners.Scope.RealmId.ToByteArray()))
                    throw new FormatException("An actor belongs to another realm.");
            var regions = owners.Terrain.Select(reference =>
            {
                var region = readTerrain(reference) ?? throw new FormatException("Required terrain is missing.");
                if (TerrainRegionReference.Capture(region) != reference)
                    throw new FormatException("Loaded terrain differs from its exact directory reference.");
                return TerrainRegionCodecV1.Encode(region);
            }).ToArray();
            byte[] spatial = Frame("world.spatial", Unsigned64(1), owners.Scope.WorldId.ToByteArray(),
                owners.Scope.BranchId.ToByteArray(), owners.Scope.RealmId.ToByteArray(),
                Collection("world.regions", regions),
                Collection("world.bodies", owners.Owners(ConstructionOwnerKind.BodyDefinition, 1).Select(value => value.Fields[0])),
                Collection("world.presences", actors.Select(value => value.Fields[1])));
            var space = WorldSpatialCodecV1.Decode(spatial).WithStructures(construction.GetStructures());
            var residents = actors.SelectMany(value => WorldSnapshotCodecV1.DecodeResidents(value.Fields[2])).ToArray();
            byte[] gathering = owners.Singleton(ConstructionOwnerKind.Gathering, 1)[0];
            var result = new WorldSnapshot(inventory, space, residents, StoredConstructionOwners.Hash(basis[0]),
                new WorldInstant(StoredConstructionOwners.Signed(timing[0]), checked((int)StoredConstructionOwners.Number(timing[1]))),
                owners.Revision, StoredConstructionOwners.Number(timing[2]),
                WorldSnapshotCodecV1.DecodeDialogues(owners.Singleton(ConstructionOwnerKind.Dialogues, 1)[0]),
                gathering.Length == 0 ? null : GatheringCodecV1.Decode(gathering), production,
                ProgressionCodecV1.Decode(owners.Singleton(ConstructionOwnerKind.Progression, 1)[0]),
                QuestCodecV1.Decode(owners.Singleton(ConstructionOwnerKind.Quests, 1)[0]), construction);
            _ = WorldSnapshotCodecV5.Encode(result); // Preserve the full-world admission/storage envelope.
            if (!ConstructionOwnerDirectory.Capture(result).CopyBytes().AsSpan().SequenceEqual(original))
                throw new FormatException("Reconstructed owners differ from the complete canonical directory.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Stored owners violate their complete world admission cohort.", error);
        }
    }

    private static CountedAuthorityState RestoreInventory(StoredConstructionOwners owners, byte[][] basis)
    {
        var custodians = owners.Owners(ConstructionOwnerKind.Custodian, 5).ToArray();
        var lots = custodians.SelectMany(value => StoredConstructionOwners.Collection(value.Fields[3],
            "owner.counted-lots", CustodySnapshot.MaximumLots)).OrderBy(CountedLotIdentity);
        var custody = Frame("custody.snapshot.v1", owners.Scope.WorldId.ToByteArray(), owners.Scope.BranchId.ToByteArray(),
            owners.Singleton(ConstructionOwnerKind.InventoryLedger, 1)[0], Collection("custody.storages.v1", custodians.Select(value => value.Fields[0])),
            Collection("custody.lots.v1", lots));
        var sources = StoredConstructionOwners.Collection(basis[4], "owner.sources", CountedAuthorityState.MaximumSources)
            .Select(value => Collection("authority.source", StoredConstructionOwners.Collection(value, "owner.source", 4)));
        var image = Frame("authority.counted-state", Unsigned64(1), basis[1], custody,
            Collection("authority.actors", StoredConstructionOwners.Collection(basis[2], "owner.admitted-actors", CountedAuthorityState.MaximumActors)),
            Collection("authority.controls", owners.Owners(ConstructionOwnerKind.Control, 2).Select(value => Frame("authority.control",
                value.Fields[0], value.Fields[1], value.Key.SubjectId.ToByteArray()))),
            Collection("authority.policies", custodians.Select(value => Frame("authority.policy", value.Key.SubjectId.ToByteArray(),
                value.Fields[1], value.Fields[2]))), Collection("authority.sources", sources),
            Collection("authority.reservations", StoredConstructionOwners.Collection(basis[3], "owner.reservations", CountedAuthorityState.MaximumReservations)));
        return AuthorityEncodingV1.Decode(image);
    }

    private static ProductionSnapshot RestoreProduction(StoredConstructionOwners owners)
    {
        var history = owners.Singleton(ConstructionOwnerKind.ProductionHistory, 3);
        var lots = owners.Owners(ConstructionOwnerKind.Custodian, 5).SelectMany(value => StoredConstructionOwners.Collection(value.Fields[4],
            "owner.produced-lots", ProductionSnapshot.MaximumLots)).Select(value => ProductionCodecV1.DecodeLot(value))
            .OrderBy(value => value.LotId).Select(ProductionCodecV1.EncodeLot);
        return ProductionCodecV1.Decode(Frame("production.snapshot", Unsigned64(1), owners.Scope.WorldId.ToByteArray(),
            owners.Scope.BranchId.ToByteArray(), history[0],
            Collection("production.recipes", owners.Owners(ConstructionOwnerKind.Recipe, 1).Select(value => value.Fields[0])),
            Collection("production.batches", owners.Owners(ConstructionOwnerKind.ProductionBatch, 1).Select(value => value.Fields[0])),
            Collection("production.lots", lots)));
    }

    private static ConstructionSnapshot RestoreConstruction(StoredConstructionOwners owners) =>
        ConstructionCodecV1.Decode(Frame("construction.snapshot", Unsigned64(1), owners.Scope.WorldId.ToByteArray(),
            owners.Scope.BranchId.ToByteArray(), owners.Singleton(ConstructionOwnerKind.ConstructionCatalog, 2)[0],
            Collection("construction.definitions", owners.Owners(ConstructionOwnerKind.ConstructionDefinition, 1).Select(value => value.Fields[0])),
            Collection("construction.projects", owners.Owners(ConstructionOwnerKind.Project, 1).Select(value => value.Fields[0]))));

    internal static EntityId CountedLotIdentity(byte[] bytes)
    {
        var reader = new WorldFrameReader(bytes, CustodyEncodingV1.MaximumSnapshotByteLength, 11);
        reader.Tag("custody.source-lot.v1"u8);
        return reader.Identity(); // The established custody decoder validates the complete record next.
    }

    private static byte[] Collection(string tag, IEnumerable<byte[]> values) =>
        Frame(tag, values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
}
