using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Immutable geometric continuation for an intact moving group and its affected actors. Each work unit
/// executes a base contact, member sweep, rider sweep, or actor footing/landing query. Dependency
/// preparation, bounded array copies and final complete-scene admission are not sliced latency claims.
/// Partial placements are private calculations and never an admitted spatial state.
/// </summary>
public sealed class ConstructionSettlementResolution
{
    public const int DefaultWorkBudget = 16;
    public const int MaximumWorkBudget = 256;
    public const int MaximumWorkUnits = 2 * ConstructionDefinition.MaximumMembers + 2 * LocalSpatialSnapshot.MaximumActors;
    private readonly ConstructionSettlementScene? _scene;
    private readonly Progress? _progress;
    private readonly ConstructionDescentAssessment? _eventAssessment;

    private ConstructionSettlementResolution(ConstructionSettlementScene scene, Progress progress)
    {
        _scene = scene;
        _progress = progress;
    }

    private ConstructionSettlementResolution(ConstructionDescentAssessment assessment)
    {
        _eventAssessment = assessment;
    }

    public ContentHash InputPin => _eventAssessment?.InputPin ?? _scene!.InputPin;
    public int MaximumTotalWorkUnits => _eventAssessment?.MaximumTotalWorkUnits ?? _scene!.MaximumWorkUnits;
    internal bool IsCoupled => IsEventOrdered || _scene!.IsCoupled;
    internal bool IsEventOrdered => _eventAssessment is not null;
    public ulong CompletedWorkUnits => _eventAssessment?.CompletedWorkUnits ?? _progress!.CompletedWorkUnits;
    /// <summary>True for either a completed candidate or a terminal refusal.</summary>
    public bool IsComplete => _eventAssessment?.IsComplete ?? _progress!.Phase is Phase.Complete or Phase.Failed;
    public bool IsFailed => _eventAssessment?.IsFailed ?? _progress!.Phase == Phase.Failed;
    public string? FailureReason => IsEventOrdered ? _eventAssessment!.FailureReason : _progress!.FailureReason;
    public ConstructionSettlementResult Result => IsEventOrdered ? _eventAssessment!.Result : _progress!.Result
        ?? throw new InvalidOperationException("Only successful completed settlement has an admitted geometric result.");

    public static ConstructionSettlementResolution Begin(LocalSpatialSnapshot space,
        IEnumerable<TerrainRegionSnapshot> candidateRegions, ConstructionPhysicalSnapshot physical,
        StructuralSettlementPolicy policy)
    {
        var scene = new ConstructionSettlementScene(space, candidateRegions, physical, policy);
        return Prepare(scene);
    }

    public static ConstructionSettlementResolution BeginCoupled(WorldSnapshot basis,
        IEnumerable<TerrainRegionSnapshot> candidateRegions)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != 17)
        {
            throw new ArgumentException("Common-trajectory coupled settlement requires its original world interpretation.", nameof(basis));
        }
        return Prepare(new ConstructionSettlementScene(basis, candidateRegions));
    }

    public static ConstructionSettlementResolution BeginEventOrdered(WorldSnapshot basis,
        IEnumerable<TerrainRegionSnapshot> candidateRegions)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != 18)
        {
            throw new ArgumentException("Event-ordered settlement requires its explicitly admitted world interpretation.", nameof(basis));
        }
        return new(ConstructionDescentAssessment.Begin(basis, candidateRegions));
    }

    private static ConstructionSettlementResolution Prepare(ConstructionSettlementScene scene)
    {
        var progress = new Progress(scene);
        try
        {
            scene.Prepare();
            progress.Phase = scene.Moving is null ? Phase.Actors : Phase.Bases;
            progress.Normalize(scene);
        }
        catch (Exception error) when (IsGeometricFailure(error))
        {
            progress.Fail(error);
        }
        return new(scene, progress);
    }

    /// <summary>Executes at most workBudget real units. Forward advancement does not replay completed work.</summary>
    public ConstructionSettlementResolution Advance(int workBudget = DefaultWorkBudget)
    {
        if (workBudget is < 1 or > MaximumWorkBudget)
        {
            throw new ArgumentOutOfRangeException(nameof(workBudget));
        }
        if (IsComplete) { return this; }
        if (_eventAssessment is not null)
        {
            return new(_eventAssessment.Advance(workBudget));
        }
        ConstructionSettlementScene scene = _scene!;
        var next = new Progress(_progress!);
        for (int unit = 0; unit < workBudget && next.Phase is not Phase.Complete and not Phase.Failed; unit++)
        {
            if (next.CompletedWorkUnits >= (ulong)scene.MaximumWorkUnits)
            {
                throw new InvalidOperationException("Settlement exceeded its proved finite geometric work bound.");
            }
            next.CompletedWorkUnits++;
            try
            {
                next.PerformUnit(scene);
                next.Normalize(scene);
            }
            catch (Exception error) when (IsGeometricFailure(error))
            {
                next.Fail(error);
            }
        }
        return new(scene, next);
    }

    private static bool IsGeometricFailure(Exception error) => error is ArgumentException or InvalidOperationException or OverflowException;
    private enum Phase { Bases, Members, Riders, Actors, Complete, Failed }

    /// <summary>Owned by one immutable resolution; an Advance clones it before performing work.</summary>
    private sealed class Progress
    {
        internal Phase Phase;
        internal int Cursor;
        internal long? Drop;
        internal ulong CompletedWorkUnits;
        internal ActorPresence[] Actors;
        internal ConstructionPhysicalSnapshot Physical;
        internal ConstructionSettlementResult? Result;
        internal string? FailureReason;

        internal Progress(ConstructionSettlementScene scene)
        {
            Actors = scene.OriginalSpace.Actors.ToArray();
            Physical = scene.OriginalPhysical;
        }

        internal Progress(Progress previous)
        {
            Phase = previous.Phase;
            Cursor = previous.Cursor;
            Drop = previous.Drop;
            CompletedWorkUnits = previous.CompletedWorkUnits;
            Actors = (ActorPresence[])previous.Actors.Clone();
            Physical = previous.Physical;
            Result = previous.Result;
            FailureReason = previous.FailureReason;
        }

        internal void Fail(Exception error)
        {
            FailureReason = error.Message;
            Phase = Phase.Failed;
            Result = null;
        }

        internal void PerformUnit(ConstructionSettlementScene scene)
        {
            switch (Phase)
            {
                case Phase.Bases:
                    VerticalDescentResult contact = scene.MovingScene!.QueryBox(scene.Bases[Cursor], scene.Policy.MaximumDropSubunits);
                    if (contact.Status != VerticalDescentStatus.Landed || contact.DropSubunits <= 0
                        || Drop is not null && Drop != contact.DropSubunits)
                    {
                        throw new ArgumentException("Rigid settling requires equal complete first contact beneath every lowest member.");
                    }
                    Drop = contact.DropSubunits;
                    break;
                case Phase.Members:
                    ConstructionSettlementScene.ValidateSweep(scene.MovingScene!, scene.MovingBoxes[Cursor], Drop!.Value);
                    break;
                case Phase.Riders:
                    ActorPresence rider = scene.Riders[Cursor];
                    ConstructionSettlementScene.ValidateSweep(scene.MovingScene!, ConstructionSettlementScene.Box(
                        BodyVolume.At(rider.Feet, rider.Condition.Definition.ClearanceDimensions)), Drop!.Value);
                    break;
                case Phase.Actors:
                    LandActor(scene);
                    break;
                default:
                    throw new InvalidOperationException("A terminal settlement cannot execute geometric work.");
            }
            Cursor++;
        }

        internal void Normalize(ConstructionSettlementScene scene)
        {
            // State transitions follow actual work; they never manufacture work-budget increments.
            if (Phase == Phase.Bases && Cursor == scene.Bases.Length)
            {
                long drop = Drop ?? throw new ArgumentException("A settling assembly requires a retained base.");
                if (drop > scene.Policy.SafeMemberDropSubunits || scene.Riders.Length != 0 && drop > scene.Policy.SafeActorDropSubunits)
                {
                    throw new ArgumentException("The contact requires damage or injury beyond the harmless settlement calibration.");
                }
                _ = scene.Policy.FlightMilliseconds(drop);
                Phase = Phase.Members;
                Cursor = 0;
            }
            if (Phase == Phase.Members && Cursor == scene.MovingBoxes.Length)
            {
                Phase = Phase.Riders;
                Cursor = 0;
            }
            if (Phase == Phase.Riders && Cursor == scene.Riders.Length)
            {
                long drop = Drop!.Value;
                Physical = Physical.WithMembers(Physical.Revision, Physical.Members
                    .Where(member => scene.IsMoving(member.StructureId))
                    .Select(member => member.WithState(member.Revision, member.ConditionParts,
                        member.Displacement.Offset(0, 0, -drop), ConstructionMemberDisposition.Resting)));
                Actors = Actors.Select(actor => scene.IsRider(actor.ActorId)
                    ? actor.At(actor.Feet.Offset(0, 0, -drop)) : actor).ToArray();
                Phase = Phase.Actors;
                Cursor = 0;
            }
            if (Phase == Phase.Actors && Cursor == Actors.Length)
            {
                var candidate = new LocalSpatialSnapshot(scene.OriginalSpace.Scope, scene.Regions, Actors,
                    Physical.GetStructures(), scene.OriginalSpace.GroundSupport, scene.OriginalSpace.StructureSupport);
                Physical.ValidateStableGeometry(candidate);
                Result = new(candidate, Physical);
                Phase = Phase.Complete;
            }
        }

        private void LandActor(ConstructionSettlementScene scene)
        {
            ActorPresence actor = Actors[Cursor];
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            long stepHeight = Math.Min(LocalSpatialSnapshot.MaximumOrdinaryStepHeightSubunits,
                actor.Condition.Definition.ClearanceDimensions.HeightSubunits / 4);
            if (FootingQuery.QueryStepped(scene.Terrain, Physical.GetStructures(), volume, stepHeight) == SpatialOccupancy.Clear)
            {
                return;
            }
            if (scene.IsRider(actor.ActorId))
            {
                throw new ArgumentException("A rigid rider must retain complete contact with its settled assembly.");
            }
            var descent = new VerticalDescentQuery(scene.OriginalSpace.Scope, scene.Regions, Physical.GetStructures(), Actors);
            VerticalDescentResult contact = descent.QueryActor(actor.ActorId, scene.Policy.MaximumDropSubunits);
            if (contact.Status != VerticalDescentStatus.Landed || contact.DropSubunits <= 0
                || contact.DropSubunits > scene.Policy.SafeActorDropSubunits)
            {
                throw new ArgumentException("The affected actor requires unresolved collision, flight or injury handling.");
            }
            _ = scene.Policy.FlightMilliseconds(contact.DropSubunits);
            Actors[Cursor] = actor.At(actor.Feet.Offset(0, 0, -contact.DropSubunits));
        }
    }
}
