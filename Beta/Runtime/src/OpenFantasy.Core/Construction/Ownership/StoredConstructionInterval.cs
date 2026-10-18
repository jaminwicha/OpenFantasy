using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.CanonicalEncodingV1;
using static OpenFantasy.Core.Construction.Ownership.StoredConstructionWorkOwners;

namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>
/// Exact admitted metadata for partial construction publication. All resident exposure and gathering
/// remain due; unrelated material detail stays in storage. Only its own committed steps renew this interval.
/// </summary>
public sealed class StoredConstructionInterval
{
    private readonly ConstructionWorkCohort _work;
    private readonly TerrainScope _scope;
    private readonly TerrainRegionReference[] _terrain;
    private readonly Dictionary<ConstructionOwnerKey, OwnerImageReference> _owners;
    private readonly HashSet<ConstructionOwnerKey> _required;
    private readonly int _completeByteLength;
    private readonly byte[] _directory;

    private StoredConstructionInterval(WorldSnapshot world, WorldActionCommand command)
    {
        _work = ConstructionWorkCohort.Capture(world, command);
        var directory = ConstructionOwnerDirectory.Capture(world);
        _scope = directory.Scope;
        _terrain = world.Space.Regions.Select(TerrainRegionReference.Capture).ToArray();
        _owners = directory.Owners.ToDictionary(owner => owner.Key,
            owner => new OwnerImageReference(owner.Hash, owner.ByteLength));
        _required = _work.RequiredOwners.Select(owner => owner.Key).ToHashSet();
        foreach (var key in _owners.Keys.Where(key => key.Kind is ConstructionOwnerKind.Actor
            or ConstructionOwnerKind.Timing or ConstructionOwnerKind.Gathering or ConstructionOwnerKind.ConstructionCatalog))
            _required.Add(key);
        _completeByteLength = WorldSnapshotCodec.Encode(world).Length;
        _directory = directory.CopyBytes();
        DirectoryHash = directory.Hash;
        Revision = world.Revision;
        AdvancingSteps = world.AdvancingSteps;
    }

    private StoredConstructionInterval(StoredConstructionInterval source, ulong steps,
        IReadOnlyList<ConstructionOwnerPayload> changed)
    {
        _work = source._work;
        _scope = source._scope;
        _terrain = source._terrain;
        _required = source._required;
        _owners = new(source._owners);
        long difference = 0;
        foreach (var owner in changed)
        {
            if (owner.ByteLength > ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength)
                throw new InvalidOperationException("Changed work owner exceeds its payload envelope.");
            difference += owner.ByteLength - _owners[owner.Key].ByteLength;
            _owners[owner.Key] = new(owner.Hash, owner.ByteLength);
        }
        // Only existing project, gathering, timing and actor-resident fields change here. Each appears
        // once in the complete profile-five frame; fixed-width revisions and frame lengths add no delta.
        _completeByteLength = checked((int)(source._completeByteLength + difference));
        if (_completeByteLength <= 0 || _completeByteLength > WorldSnapshotCodec.MaximumStateByteLength
            || _owners.Values.Sum(owner => (long)owner.ByteLength) > ConstructionOwnerDirectory.MaximumTotalPayloadByteLength)
            throw new InvalidOperationException("Partial work exceeds its complete admitted byte envelope.");
        Revision = checked(source.Revision + 1);
        AdvancingSteps = steps;
        _directory = ConstructionOwnerDirectory.EncodeReferences(_scope, Revision, _terrain,
            _owners.OrderBy(pair => pair.Key.Kind).ThenBy(pair => pair.Key.SubjectId)
                .Select(pair => new ConstructionOwnerReference(pair.Key, pair.Value.Hash)));
        DirectoryHash = ContentHash.Compute(_directory);
    }

    public ContentHash DirectoryHash { get; }
    public ulong Revision { get; }
    public ulong AdvancingSteps { get; }
    public int CompleteOwnerCount => _owners.Count;
    public int RequiredOwnerCount => _required.Count;
    public byte[] CopyDirectory() => _directory.ToArray();
    public static StoredConstructionInterval Capture(WorldSnapshot world, WorldActionCommand command) => new(world, command);

    /// <summary>
    /// Prepares one exact selected step. Null requires complete material/geometry admission before any
    /// publication. The caller owns serialization, historical identity authentication and durable writes.
    /// </summary>
    public StoredConstructionStep? Prepare(WorldStepBatch batch, ContentHash currentDirectory,
        Func<ContentHash, byte[]> readOwner, Func<EntityId, bool> historicallyOccupied)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(readOwner);
        ArgumentNullException.ThrowIfNull(historicallyOccupied);
        if (currentDirectory != DirectoryHash || batch.ProfileVersion != 5
            || batch.WorldId != _scope.WorldId || batch.BranchId != _scope.BranchId
            || batch.ExpectedRevision != Revision || batch.ExpectedAdvancingSteps != AdvancingSteps
            || batch.Movement.Count != 0 || batch.Actions.Count != 1
            || batch.Kind is not WorldBatchKind.AdvancingStep and not WorldBatchKind.HeldInteraction)
            throw new InvalidOperationException("Selected work requires the exact current admitted interval.");
        var command = batch.Actions[0];
        _work.ValidateCommand(command);
        var fields = ReadFields(readOwner);
        var source = new StoredConstructionDecisionSource(_scope, command, fields);
        if (!source.Authorizes(command) || (batch.Kind == WorldBatchKind.HeldInteraction) != source.HasReadingHold)
            throw new InvalidOperationException("Selected work requires its current controller and hold.");
        var timingKey = new ConstructionOwnerKey(ConstructionOwnerKind.Timing, _scope.WorldId);
        var timing = Get(fields, timingKey, 3);
        if (StoredConstructionOwners.Number(timing[2]) != AdvancingSteps)
            throw new FormatException("Stored timing differs from the admitted interval.");
        var catalogKey = new ConstructionOwnerKey(ConstructionOwnerKind.ConstructionCatalog, _scope.WorldId);
        var catalog = Get(fields, catalogKey, 2);
        ulong constructionRevision = StoredConstructionOwners.Number(catalog[0]);
        var reservations = new WorldStepIdentityReservations(historicallyOccupied);
        reservations.Reserve(batch.CauseId, WorldCommittedIdentityKind.CommitCause);
        reservations.Reserve(command.CommandId, WorldCommittedIdentityKind.Command);
        var qualification = _work.Qualify(fields, command, constructionRevision,
            batch.Kind == WorldBatchKind.AdvancingStep, source.HasReadingHold);
        if (qualification.Work is { } finishing
            && finishing.Progress.CompletedUnits >= finishing.Project.Definition.RequiredWork.Value)
            return null;

        List<ConstructionOwnerPayload> changed = [];
        void Replace(ConstructionOwnerKey key, params byte[][] values)
        {
            var payload = ConstructionOwnerPayload.Create(key, _scope, values);
            if (payload.Hash != _owners[key].Hash) changed.Add(payload);
        }
        var rejection = qualification.Rejection;
        if (qualification.Work is { } work)
        {
            try
            {
                var project = work.Project.WithProgress(work.Progress, null);
                ulong nextConstructionRevision = checked(constructionRevision + 1);
                Replace(new(ConstructionOwnerKind.Project, project.ProjectId), ConstructionCodecV1.EncodeProject(project));
                Replace(catalogKey, Unsigned64(nextConstructionRevision), catalog[1]);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            {
                changed.Clear();
                rejection = WorldActionRejection.Unavailable;
            }
        }
        ulong steps = AdvancingSteps;
        if (batch.Kind == WorldBatchKind.AdvancingStep)
        {
            var calendar = new WorldInstant(StoredConstructionOwners.Signed(timing[0]),
                checked((int)StoredConstructionOwners.Number(timing[1])));
            var boundary = new WorldExplorationBoundary(calendar, AdvancingSteps, null, null);
            steps = boundary.AdvancingSteps;
            Replace(timingKey, WorldSpatialCodecV1.Signed(boundary.Calendar.Days), Unsigned64((ulong)boundary.Calendar.MillisecondsWithinDay), Unsigned64(steps));
            var gatheringKey = new ConstructionOwnerKey(ConstructionOwnerKind.Gathering, _scope.WorldId);
            var gathering = Get(fields, gatheringKey, 1)[0];
            if (gathering.Length != 0)
                Replace(gatheringKey, GatheringCodecV1.Encode(GatheringCodecV1.Decode(gathering).AdvanceStep(steps)));
            foreach (var pair in fields.Where(pair => pair.Key.Kind == ConstructionOwnerKind.Actor))
            {
                var actor = Get(fields, pair.Key, 3);
                var residents = WorldSnapshotCodecV1.DecodeResidents(actor[2]);
                var exposure = new WorldResidentExposureBasis(residents.AsReadOnly(), AdvancingSteps).AdvanceTo(steps);
                Replace(pair.Key, actor[0], actor[1], WorldSnapshotCodecV1.EncodeResidents(exposure.Materialize()));
            }
        }
        var receipt = WorldActionReceipt.Create(command, checked(Revision + 1), rejection);
        reservations.Reserve(receipt.ResultId, WorldCommittedIdentityKind.Result);
        return new(new(this, steps, changed), receipt, changed, reservations.Identities);
    }

    private Dictionary<ConstructionOwnerKey, byte[][]> ReadFields(Func<ContentHash, byte[]> readOwner)
    {
        Dictionary<ConstructionOwnerKey, byte[][]> fields = [];
        long bytes = 0;
        foreach (var key in _required.OrderBy(key => key.Kind).ThenBy(key => key.SubjectId))
        {
            var owner = _owners[key];
            byte[] payload = readOwner(owner.Hash) ?? throw new FormatException("A required work owner is missing.");
            bytes = checked(bytes + payload.Length);
            if (payload.Length != owner.ByteLength || bytes > ConstructionOwnerDirectory.MaximumTotalPayloadByteLength)
                throw new FormatException("Required work owners differ from their admitted byte envelope.");
            fields.Add(key, StoredConstructionOwners.ReadFields(payload, key, _scope, owner.Hash));
        }
        return fields;
    }

    private sealed record OwnerImageReference(ContentHash Hash, int ByteLength);
}

/// <summary>An unpublished directory update and its changed payloads, never a second durable writer.</summary>
public sealed class StoredConstructionStep
{
    internal StoredConstructionStep(StoredConstructionInterval candidate, WorldActionReceipt receipt,
        IEnumerable<ConstructionOwnerPayload> changed, IEnumerable<WorldCommittedIdentity> identities)
    {
        Candidate = candidate;
        Receipt = receipt;
        ChangedOwners = Array.AsReadOnly(changed.ToArray());
        Identities = Array.AsReadOnly(identities.OrderBy(value => value.Identity).ToArray());
    }
    public StoredConstructionInterval Candidate { get; }
    public WorldActionReceipt Receipt { get; }
    public ReadOnlyCollection<ConstructionOwnerPayload> ChangedOwners { get; }
    public ReadOnlyCollection<WorldCommittedIdentity> Identities { get; }
}
