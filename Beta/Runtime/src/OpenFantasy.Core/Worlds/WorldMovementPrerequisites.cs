using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Joined exploration-movement reads from an admitted world. These prerequisites can evaluate geometry,
/// but never replace the selected step's action ordering, due work, complete admission or publication.
/// </summary>
public sealed class WorldMovementPrerequisites
{
    private readonly Boundary _boundary;

    private WorldMovementPrerequisites(WorldSnapshot world, WorldMovementInput input, int maximumLoadedRegions)
    {
        Input = input;
        var actor = world.Space.GetActor(input.ActorId);
        Spatial = SpatialMovementDependencies.Capture(world.Space, input.ActorId, actor.Revision,
            input.DeltaX, input.DeltaY, maximumLoadedRegions);
        Control = WorldActorControlDependencies.Capture(world, input.ActorId, input.Delivery,
            input.ControlWitness, WorldControlCapabilities.Traversal);
        Carriage = WorldActorCarriage.Capture(world, input.ActorId);
        _boundary = ReadBoundary(world, input.ActorId);
    }

    public WorldMovementInput Input { get; }
    public SpatialMovementDependencies Spatial { get; }
    public WorldActorControlDependencies Control { get; }
    public WorldActorCarriage Carriage { get; }

    /// <summary>Eligibility at capture only; successful geometry still needs complete world admission.</summary>
    public bool CanAttempt => Control.IsAuthorized && _boundary.CanAct && !_boundary.ReadingHold
        && !_boundary.PendingCare && !_boundary.PendingExcavation && !_boundary.ActiveTiming
        && !_boundary.EncounterActions && _boundary.Phase is null or EncounterLifecyclePhase.Exploration;

    public static WorldMovementPrerequisites Capture(WorldSnapshot world, WorldMovementInput input, int maximumLoadedRegions)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(input);
        return new(world, input, maximumLoadedRegions);
    }

    /// <summary>Produces a geometric candidate, not an accepted action, earned time or durable receipt.</summary>
    public SpatialMoveResult Evaluate(TerrainReadSet terrain)
    {
        if (!CanAttempt)
        {
            throw new InvalidOperationException("The captured boundary does not permit an ordinary movement attempt.");
        }
        return Spatial.Evaluate(terrain);
    }

    /// <summary>Must be serialized with all other selected-step checks and publication by the owning authority.</summary>
    public void ValidateCurrent(WorldSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        Control.ValidateCurrent(current);
        Carriage.ValidateCurrent(current);
        Spatial.ValidateCurrent(current.Space);
        if (_boundary != ReadBoundary(current, Input.ActorId))
        {
            throw new InvalidOperationException("The prepared movement time, readiness or hold boundary changed.");
        }
    }

    private static Boundary ReadBoundary(WorldSnapshot world, EntityId actorId) => new(
        world.Calendar, world.AdvancingSteps, world.GetResident(actorId).ExperiencedTime,
        world.Pacing?.ContentHash, world.EncounterLifecycle?.Phase,
        world.EncounterLifecycle?.Revision, world.EncounterLifecycle?.GetDebt(actorId).RemainingDelay,
        world.EncounterLifecycle is null ? null : world.Progression!.GetActor(actorId).CurrentHp,
        WorldActorAvailability.CanAct(world, actorId), world.HasReadingHold, world.Care?.Pending is not null,
        world.PendingExcavation is not null, world.Pacing?.Encounter is not null, world.EncounterActions is not null,
        world.StructuralPolicy?.ContentHash);

    private readonly record struct Boundary(WorldInstant Calendar, ulong AdvancingSteps,
        LocalExposureDuration ExperiencedTime, ContentHash? Pacing, EncounterLifecyclePhase? Phase,
        ulong? LifecycleRevision, ulong? RemainingDelay, int? CurrentHp, bool CanAct, bool ReadingHold,
        bool PendingCare, bool PendingExcavation, bool ActiveTiming, bool EncounterActions, ContentHash? StructuralPolicy);
}
