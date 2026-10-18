using OpenFantasy.Core.Actors;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.CanonicalEncodingV1;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>
/// Complete bounded owner directory for the legacy construction interpretation. Images are verified
/// against ordinary replay; this type supplies no independent admission decoder or world scheduler.
/// </summary>
public sealed class ConstructionOwnerDirectory
{
    public const int MaximumDirectoryByteLength = 1024 * 1024;
    public const int MaximumOwnerPayloadByteLength = WorldSnapshotCodec.MaximumStateByteLength;
    public const int MaximumTotalPayloadByteLength = 32 * 1024 * 1024;
    private readonly byte[] _bytes;

    private ConstructionOwnerDirectory(TerrainScope scope, ulong revision,
        IEnumerable<TerrainRegionReference> terrain, IEnumerable<ConstructionOwnerPayload> owners)
    {
        Scope = scope;
        Revision = revision;
        Owners = Array.AsReadOnly(owners.OrderBy(owner => owner.Key.Kind).ThenBy(owner => owner.Key.SubjectId).ToArray());
        if (Owners.Select(owner => owner.Key).Distinct().Count() != Owners.Count
            || Owners.Any(owner => owner.ByteLength > MaximumOwnerPayloadByteLength)
            || Owners.Sum(owner => (long)owner.ByteLength) > MaximumTotalPayloadByteLength)
            throw new ArgumentException("Owner images exceed their exact-key or byte envelope.", nameof(owners));
        var references = terrain.Select(value => Frame("world.owner-terrain.v1",
            Signed(value.Bounds.Minimum.X), Signed(value.Bounds.Minimum.Y), Signed(value.Bounds.Minimum.Z),
            Number((ulong)value.Bounds.Width), Number((ulong)value.Bounds.Depth), Number((ulong)value.Bounds.Height),
            Number(value.Revision), value.ContentHash.ToByteArray()));
        _bytes = Frame("world.construction-owners.v1", Number(1), Number(5), Id(scope.WorldId), Id(scope.BranchId),
            Id(scope.RealmId), Number(revision), Collection("world.owner-terrain", references),
            Collection("world.owner-entries", Owners.Select(owner => Frame("world.owner-entry.v1",
                Number((ulong)owner.Key.Kind), Id(owner.Key.SubjectId), owner.Hash.ToByteArray()))));
        if (_bytes.Length > MaximumDirectoryByteLength)
            throw new ArgumentException("Owner directory exceeds its retained metadata envelope.", nameof(owners));
        Hash = ContentHash.Compute(_bytes);
    }

    public TerrainScope Scope { get; }
    public ulong Revision { get; }
    public ContentHash Hash { get; }
    public ReadOnlyCollection<ConstructionOwnerPayload> Owners { get; }
    public byte[] CopyBytes() => _bytes.ToArray();

    public static ConstructionOwnerDirectory Capture(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 5) throw new ArgumentException("Typed construction owners require profile five.", nameof(world));
        return Capture(world.Inventory, world.Space.Scope, world.Space.Actors, world.Residents,
            world.Space.Regions.Select(TerrainRegionReference.Capture), world.AdmissionVersion, world.Calendar,
            world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering,
            world.Production!, world.Progression!, world.Quests!, world.Construction!);
    }

    public static ConstructionOwnerDirectory Capture(ConstructionLocalityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Capture(state.Inventory, state.Occupancy.Scope, state.Actors, state.Residents, state.Terrain,
            state.AdmissionVersion, state.Calendar, state.Revision, state.AdvancingSteps, state.Dialogues,
            state.Gathering, state.Production, state.Progression, state.Quests, state.Construction);
    }

    private static ConstructionOwnerDirectory Capture(CountedAuthorityState inventory, TerrainScope scope,
        IEnumerable<ActorPresence> actors, IEnumerable<WorldResident> residents, IEnumerable<TerrainRegionReference> terrain,
        ContentHash admission, WorldInstant calendar, ulong revision, ulong steps, IEnumerable<DialogueSession> dialogues,
        GatheringSnapshot? gathering, ProductionSnapshot production, ProgressionSnapshot progression,
        QuestSnapshot quests, ConstructionSnapshot construction)
    {
        if (production.SupportsConsumption || production.IsContinued || production.MaterialHistory is not null)
            throw new ArgumentException("Typed legacy owners cannot omit richer production history.", nameof(production));
        List<ConstructionOwnerPayload> owners = [];
        void Add(ConstructionOwnerKind kind, EntityId subject, params ReadOnlyMemory<byte>[] fields)
        {
            var header = new ReadOnlyMemory<byte>[] { Number(1), Number((ulong)kind), Id(scope.WorldId), Id(scope.BranchId), Id(subject) };
            owners.Add(new(new(kind, subject), Frame("world.construction-owner.v1", header.Concat(fields).ToArray())));
        }
        Add(ConstructionOwnerKind.InventoryBasis, scope.WorldId, admission.ToByteArray(), Number(inventory.AdmittedUnitCapacity),
            Collection("owner.admitted-actors", inventory.Actors.Select(Id)),
            Collection("owner.reservations", inventory.ReservationIds.Select(Id)),
            Collection("owner.sources", inventory.Sources.Select(source => Frame("owner.source", Id(source.SourceId),
                Id(source.Definition.DefinitionId), source.Definition.Version.ToByteArray(), Number(source.UnitCapacity)))));
        Add(ConstructionOwnerKind.InventoryLedger, scope.WorldId, Number(inventory.Custody.Revision));
        Add(ConstructionOwnerKind.Timing, scope.WorldId, Signed(calendar.Days), Number((ulong)calendar.MillisecondsWithinDay), Number(steps));
        var actorValues = actors.ToArray();
        var residentValues = residents.ToArray();
        if (residentValues.Any(resident => !actorValues.Any(actor => actor.ActorId == resident.ActorId))
            || production.Lots.Any(lot => !inventory.Custody.Storages.Any(storage => storage.CustodianId == lot.CustodianId)))
            throw new ArgumentException("Typed images require every resident and produced lot to retain an actual owner.");
        foreach (var definition in actorValues.Select(actor => actor.Condition.Definition).DistinctBy(value => value.DefinitionId))
            Add(ConstructionOwnerKind.BodyDefinition, definition.DefinitionId, BodyContractEncodingV1.EncodeDefinition(definition));
        foreach (var actor in actorValues)
            Add(ConstructionOwnerKind.Actor, actor.ActorId, Id(scope.RealmId), EncodeActor(actor),
                WorldSnapshotCodecV1.EncodeResidents(residentValues.Where(value => value.ActorId == actor.ActorId)));
        foreach (var control in inventory.Controls)
            Add(ConstructionOwnerKind.Control, control.ActorId, Id(control.PrincipalId), Id(control.SessionId));
        foreach (var storage in inventory.Custody.Storages)
        {
            var policy = inventory.Policies.Single(value => value.CustodianId == storage.CustodianId);
            Add(ConstructionOwnerKind.Custodian, storage.CustodianId, CustodyEncodingV1.EncodeStorage(storage),
                Id(policy.OwnerActorId), Number((ulong)policy.DepositRule),
                Collection("owner.counted-lots", inventory.Custody.Lots.Where(lot => lot.CustodianId == storage.CustodianId).Select(CustodyEncodingV1.EncodeLot)),
                Collection("owner.produced-lots", production.Lots.Where(lot => lot.CustodianId == storage.CustodianId).Select(ProductionCodecV1.EncodeLot)));
        }
        foreach (var recipe in production.Recipes)
            Add(ConstructionOwnerKind.Recipe, recipe.DefinitionId, ProductionCodecV1.EncodeRecipe(recipe));
        foreach (var batch in production.Batches)
            Add(ConstructionOwnerKind.ProductionBatch, batch.BatchId, ProductionCodecV1.EncodeBatch(batch));
        Add(ConstructionOwnerKind.ProductionHistory, scope.WorldId, Number(production.Revision),
            Collection("owner.recipe-keys", production.Recipes.Select(value => Id(value.DefinitionId))),
            Collection("owner.batch-keys", production.Batches.Select(value => Id(value.BatchId))));
        foreach (var definition in construction.Definitions)
            Add(ConstructionOwnerKind.ConstructionDefinition, definition.DefinitionId, ConstructionCodecV1.EncodeDefinition(definition));
        Add(ConstructionOwnerKind.ConstructionCatalog, scope.WorldId, Number(construction.Revision),
            Collection("owner.construction-keys", construction.Definitions.Select(value => Id(value.DefinitionId))));
        foreach (var project in construction.Projects)
            Add(ConstructionOwnerKind.Project, project.ProjectId, ConstructionCodecV1.EncodeProject(project));
        Add(ConstructionOwnerKind.Gathering, scope.WorldId, gathering is null ? [] : GatheringCodecV1.Encode(gathering));
        Add(ConstructionOwnerKind.Progression, scope.WorldId, ProgressionCodecV1.Encode(progression));
        Add(ConstructionOwnerKind.Quests, scope.WorldId, QuestCodecV1.Encode(quests));
        Add(ConstructionOwnerKind.Dialogues, scope.WorldId, WorldSnapshotCodecV1.EncodeDialogues(dialogues));
        return new(scope, revision, terrain, owners);
    }

    private static byte[] Id(EntityId identity) => identity.ToByteArray();
    private static byte[] Collection(string tag, IEnumerable<byte[]> values) => Frame(tag, values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
}
