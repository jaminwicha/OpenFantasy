using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Actual retreat legality over one admitted world: the same control, body, locomotion, movement, complete root
/// admission and attack-centre geometry as ordinary movement and attacks. Every check reads current state; captured
/// advice is never trusted. Queueing, advice verification and every walked sample use these same rules.
/// </summary>
public static class WorldEncounterRetreats
{
    /// <summary>True when this world admits the explicit retreat interpretation.</summary>
    public static bool Admits(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.Retreat is not null;
    }

    /// <summary>True when a committed retreat currently holds this actor's decisions.</summary>
    public static bool HoldsActor(WorldSnapshot world, EntityId actorId) => world.Retreat?.Holds(actorId) == true;

    /// <summary>Living opposing participants of an actor in the current episode, in identity order.</summary>
    public static IReadOnlyList<EncounterCombatant> LivingOpponents(WorldSnapshot world, EntityId actorId)
    {
        if (world.EncounterActions is not { } actions) return [];
        ushort side = actions.GetCombatant(actorId).Side;
        return actions.Combatants.Where(value => value.Side != 0 && value.Side != side
            && world.Progression!.GetActor(value.ActorId).CurrentHp > 0).ToArray();
    }

    /// <summary>
    /// Exactly two positive sides participate. Legacy retreat requires the actor to be its side's only member,
    /// living or downed. Collective withdrawal retains the full side; no third-party fight ends through this exit.
    /// </summary>
    public static bool IsSupportedSide(WorldSnapshot world, EntityId actorId) =>
        world.Retreat?.Withdrawal is null ? EncounterRetreatRules.IsSupportedScope(world.EncounterActions!.Combatants, actorId)
        : world.EncounterActions!.GetCombatant(actorId).Side != 0
            && world.EncounterActions.Combatants.Where(actor => actor.Side != 0).Select(actor => actor.Side).Distinct().Count() == 2;

    /// <summary>The contact radius of one opponent: its longest granted ability range plus one full cell.</summary>
    public static long ContactRadius(WorldSnapshot world, EntityId opponentId)
    {
        var actions = world.EncounterActions!;
        long longest = actions.GetCombatant(opponentId).AbilityIds
            .Select(id => actions.GetAbility(id).RangeSubunits).DefaultIfEmpty(1).Max();
        return EncounterRetreatRules.ContactRadius(longest);
    }

    /// <summary>
    /// The smallest squared excess, in doubled subunits, of the horizontal distance between the actor's body centre
    /// (placed at <paramref name="feet"/>) and each living opponent's body centre over that opponent's contact radius.
    /// Positive is clear. Sightline, missing cells and floor are deliberately ignored: none of them grants an escape.
    /// </summary>
    public static Int128 ContactExcess(WorldSnapshot world, LocalSpatialSnapshot space, EntityId actorId, SubunitPosition feet)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(space);
        var center = BodyVolume.At(feet, space.GetActor(actorId).Condition.Definition.ClearanceDimensions).DoubledCenter;
        Int128 smallest = Int128.MaxValue;
        foreach (EncounterCombatant opponent in LivingOpponents(world, actorId))
        {
            ActorPresence other = space.GetActor(opponent.ActorId);
            var at = BodyVolume.At(other.Feet, other.Condition.Definition.ClearanceDimensions).DoubledCenter;
            Int128 dx = at.X - center.X;
            Int128 dy = at.Y - center.Y;
            Int128 limit = (Int128)ContactRadius(world, opponent.ActorId) * 2;
            smallest = Int128.Min(smallest, dx * dx + dy * dy - limit * limit);
        }
        return smallest;
    }

    /// <summary>Whether the actor, placed as in <paramref name="space"/>, is beyond every living opponent's contact radius.</summary>
    public static bool IsClearOfContact(WorldSnapshot world, LocalSpatialSnapshot space, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(space);
        return ContactExcess(world, space, actorId, space.GetActor(actorId).Feet) > 0;
    }

    /// <summary>
    /// One sample through the ordinary movement owner from the actor's actual feet: the moved space, or null with the
    /// refusing movement status.
    /// </summary>
    public static LocalSpatialSnapshot? WalkSample(LocalSpatialSnapshot space, EntityId actorId, EncounterRetreatDirection direction,
        out SpatialMoveStatus status)
    {
        ArgumentNullException.ThrowIfNull(space);
        (long x, long y) = QueueEncounterRetreatIntent.Displacement(direction);
        SpatialMoveResult move = space.PrepareMove(actorId, space.GetActor(actorId).Revision, x, y);
        status = move.Status;
        return move.Status == SpatialMoveStatus.Prepared
            ? space.WithActors(space.Actors.Select(other => other.ActorId == actorId ? move.Candidate! : other)) : null;
    }

    /// <summary>
    /// Complete root admission of a placement at the next boundary with every other owner retained, exactly as
    /// ordinary movement admits a step: the same rules a published sample passes (liquid dry envelopes, reservoir
    /// seals, custody, construction, identity). It never publishes anything.
    /// </summary>
    public static bool AdmitsPlacement(WorldSnapshot world, LocalSpatialSnapshot space)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(space);
        var lifecycle = world.EncounterLifecycle?.RetainBoundary();
        try
        {
            _ = world.WithState(world.Inventory, space, world.Residents, world.Calendar, checked(world.Revision + 1), world.AdvancingSteps,
                world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests, world.Construction,
                world.Reservoirs, world.Transport, world.Excavation, world.ConstructionPhysical, world.LiquidCells, world.Pacing,
                encounterLifecycle: lifecycle, resourceHistory: world.ResourceHistory, retreat: world.Retreat);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Walks the captured route sample by sample: each prefix through the movement owner and complete root admission,
    /// then contact at its end. <paramref name="admitted"/> memoises placement admission within one unchanged world.
    /// </summary>
    internal static WorldActionRejection EvaluateRoute(WorldSnapshot world, EntityId actorId, IEnumerable<EncounterRetreatDirection> route,
        Dictionary<SubunitPosition, bool>? admitted = null)
    {
        LocalSpatialSnapshot space = world.Space;
        foreach (EncounterRetreatDirection direction in route)
        {
            LocalSpatialSnapshot? next;
            try { next = WalkSample(space, actorId, direction, out _); }
            catch (Exception error) when (error is ArgumentException or OverflowException) { next = null; }
            if (next is null) return WorldActionRejection.Unavailable;
            SubunitPosition feet = next.GetActor(actorId).Feet;
            if (admitted is null || !admitted.TryGetValue(feet, out bool placed))
            {
                placed = AdmitsPlacement(world, next);
                admitted?.Add(feet, placed);
            }
            if (!placed) return WorldActionRejection.Unavailable;
            space = next;
        }
        return IsClearOfContact(world, space, actorId) ? WorldActionRejection.None : WorldActionRejection.OutOfReach;
    }

    /// <summary>
    /// Queue legality of a committed retreat. Control, actor revision, due readiness and timing revision are checked
    /// by the encounter decision boundary before this; this owner checks interpretation, scope, body, route and contact.
    /// </summary>
    public static WorldActionRejection EvaluateQueue(WorldSnapshot world, EntityId actorId, QueueEncounterRetreatIntent intent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intent);
        if (world.Retreat is not { } retreat || world.EncounterActions is null
            || world.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Active || retreat.Active is not null
            || retreat.Withdrawal?.FindMember(actorId) is not null
            || !WorldEncounterActions.CanParticipate(world, actorId) || LivingOpponents(world, actorId).Count == 0
            || !IsSupportedSide(world, actorId))
        {
            return WorldActionRejection.Unavailable;
        }
        ActorPresence actor = world.Space.GetActor(actorId);
        if (!actor.Condition.CanLocomote(LocomotionMode.Ground))
        {
            return WorldActionRejection.Unavailable;
        }
        SubunitPosition endpoint;
        try { endpoint = QueueEncounterRetreatIntent.Endpoint(actor.Feet, intent.Route); }
        catch (OverflowException) { return WorldActionRejection.Unavailable; }
        return endpoint != intent.Destination ? WorldActionRejection.RevisionConflict : EvaluateRoute(world, actorId, intent.Route);
    }

    /// <summary>Whether the command's delivery still controls the actor; a changed controller interrupts the retreat.</summary>
    internal static bool StillControls(WorldSnapshot world, WorldActionCommand command) =>
        world.EffectiveControl is not null
            ? WorldActorCommandAuthority.AuthorizeAccepted(world, command) is not null
            : world.Inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId);
}
