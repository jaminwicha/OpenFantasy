using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.RetreatCompletion;

/// <summary>
/// Complete-world admission of retreat samples over the real profile-34 terrain. The breach farm's solid divider lies
/// inside the reservoir's potential wet envelope (its spill-plane cell), so the ordinary movement owner
/// (<see cref="LocalSpatialSnapshot.PrepareMove"/>) supports a body stepping onto it while the complete root
/// (the terrain-liquid dry envelope) refuses that placement. The real first-wolf episode is reached by ordinary play;
/// its two combatants are then carried to the channel bank as MARKED COUNTERFACTUAL roots: only those actors' feet and
/// revisions (and, for the after-commit contrast, the committed walk carried rigidly with them) are replaced, every
/// other owner and invariant is the authority's own, and the roots run only through the public processor, queue
/// owner and advisor. They are never persisted or played and claim no native journey.
/// </summary>
public sealed class RetreatCompleteAdmissionTests
{
    private const string Seed = "retreat-complete-admission";
    private const string DryEnvelopeRefusal = "wet envelope";

    /// <summary>The authored stance on the north bank, directly north of the channel's dry divider.</summary>
    private static readonly SubunitPosition Bank = BreachFarmLayout.MiningStance;

    private sealed record Boundary(WorldSnapshot State, EntityId Player, EntityId Wolf);

    [Fact]
    public async Task A_sample_movement_supports_but_the_dry_envelope_refuses_is_never_queued_offered_or_walked()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-complete-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Boundary boundary = await FirstBoundary(directory);
            EntityId player = boundary.Player;
            Assert.Equal(Bank.Z, boundary.State.Space.GetActor(player).Feet.Z);
            Assert.Equal(BreachFarmLayout.DividerCell, new OpenFantasy.Core.Terrain.CellCoordinate(
                Bank.X / SubunitPosition.SubunitsPerCell, Bank.Y / SubunitPosition.SubunitsPerCell + 1, Bank.Z / SubunitPosition.SubunitsPerCell - 1));

            // MARKED COUNTERFACTUAL: the player stands at the bank stance with the real wolf one cell north of it.
            WorldSnapshot bank = Relocated(boundary.State, Placement(boundary, Bank));
            Assert.Equal(WorldEncounterRetreatAdmission.ProfileVersion, bank.ProfileVersion);
            Assert.Equal(Bank, bank.Space.GetActor(player).Feet);
            EncounterTimingSnapshot timing = bank.Pacing!.Encounter!;
            Assert.Contains(WorldEncounterActions.DueActors(bank), due => due.ActorId == player);
            Assert.DoesNotContain(timing.Decisions, decision => decision.ActorId == player);

            // The straight southward escape over the divider: every sample is prepared by the movement owner and its
            // end is clear of contact, yet one prefix lies in the wet envelope and the complete root refuses it.
            (List<EncounterRetreatDirection> south, int firstRefused) = SouthOverDivider(bank, player);
            Assert.InRange(firstRefused, 1, south.Count);
            LocalSpatialSnapshot refusedSpace = Walk(bank.Space, player, south.Take(firstRefused));
            ArgumentException cause = Assert.ThrowsAny<ArgumentException>(() => Counterfactual(bank, refusedSpace));
            Assert.Contains(DryEnvelopeRefusal, cause.Message);
            if (firstRefused > 1)
            {
                Assert.True(WorldEncounterRetreats.AdmitsPlacement(bank, Walk(bank.Space, player, south.Take(firstRefused - 1))));
            }

            // 1. Queue: refused as Unavailable with no movement, no readiness charge, no choice and no retreat record.
            SubunitPosition origin = bank.Space.GetActor(player).Feet;
            var forbidden = new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision,
                QueueEncounterRetreatIntent.Endpoint(origin, south), south);
            Assert.Equal(WorldActionRejection.Unavailable, WorldEncounterRetreats.EvaluateQueue(bank, player, forbidden));
            WorldStepDecision refused = Decide(bank, WorldBatchKind.EncounterDecision, Command(bank, player, forbidden));
            Assert.Equal(WorldActionRejection.Unavailable, refused.Receipts.Single().Rejection);
            WorldSnapshot unmoved = refused.Candidate;
            Assert.Equal(origin, unmoved.Space.GetActor(player).Feet);
            Assert.Equal(bank.Space.GetActor(player).Revision, unmoved.Space.GetActor(player).Revision);
            Assert.Equal(timing.GetActor(player).RemainingDelay, unmoved.Pacing!.Encounter!.GetActor(player).RemainingDelay);
            Assert.DoesNotContain(unmoved.Pacing.Encounter.Decisions, decision => decision.ActorId == player);
            Assert.Equal(EncounterRetreatCodecV1.Encode(bank.Retreat!), EncounterRetreatCodecV1.Encode(unmoved.Retreat!));
            Assert.Equal(bank.Progression!.GetActor(player).CurrentHp, unmoved.Progression!.GetActor(player).CurrentHp);
            Assert.Equal(EncounterLifecyclePhase.Active, unmoved.EncounterLifecycle!.Phase);

            // 2. Advice: the fewer-sample southward route is excluded; a longer legal route along the bank is found,
            // every prefix of which the complete root admits, and the host offers exactly that route.
            EncounterRetreatAdvice advice = WorldEncounterRetreatAdvisor.Advise(bank, player);
            Assert.True(advice.IsFound, $"Another legal route must exist: {advice.Status}.");
            Assert.True(advice.Route.Count > south.Count,
                $"The advisor prefers fewest samples, so the {south.Count}-sample southward route was excluded only by admission ({advice.Route.Count}).");
            LocalSpatialSnapshot walked = bank.Space;
            foreach (EncounterRetreatDirection direction in advice.Route)
            {
                walked = WalkSample(walked, player, direction);
                Assert.True(WorldEncounterRetreats.AdmitsPlacement(bank, walked));
            }
            Assert.True(WorldEncounterRetreats.IsClearOfContact(bank, walked, player));
            Assert.NotEqual(forbidden.Destination, advice.Destination);
            LocalRetreatOffer presented = new LocalWorldView(bank, player).Retreat!.Offer!;
            Assert.True(presented.CanQueue, $"The bank offer must be executable: {presented.BlockedReason}.");
            Assert.Equal(advice.Route, presented.Route);
            WorldStepDecision accepted = Decide(bank, WorldBatchKind.EncounterDecision, Command(bank, player,
                new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision, advice.Destination, advice.Route)));
            Assert.Equal(WorldActionRejection.None, accepted.Receipts.Single().Rejection);
            Assert.Equal(1UL, accepted.Candidate.Retreat!.AcceptedCount);

            // 3. After commitment: a straight southward retreat committed where it is legal (the same geometry whole cells
            // north) and walked one real sample, then carried rigidly so the envelope now lies across its next sample.
            WorldSnapshot commitPlace = CommitPlace(boundary, south, out long shiftCells);
            SubunitPosition commitOrigin = commitPlace.Space.GetActor(player).Feet;
            EncounterTimingSnapshot commitTiming = commitPlace.Pacing!.Encounter!;
            WorldActionCommand retreat = Command(commitPlace, player, new QueueEncounterRetreatIntent(commitTiming.EncounterId,
                commitTiming.Revision, QueueEncounterRetreatIntent.Endpoint(commitOrigin, south), south));
            WorldStepDecision queued = Decide(commitPlace, WorldBatchKind.EncounterDecision, retreat);
            Assert.Equal(WorldActionRejection.None, queued.Receipts.Single().Rejection);
            WorldSnapshot walking = UntilDue(queued.Candidate, player, value => value.Retreat!.Active is { HasStarted: true });
            EncounterRetreatContinuation active = walking.Retreat!.Active!;
            Assert.Equal(retreat.CommandId, active.Command.CommandId);
            Assert.Equal(EncounterRetreatDirection.South, active.NextDirection);
            Assert.True(active.SamplesTaken < south.Count - 1, "The committed walk must have samples left after the refused one.");

            SubunitPosition lastAdmitted = QueueEncounterRetreatIntent.Endpoint(Bank, south.Take(firstRefused - 1));
            WorldSnapshot carried = Carried(walking, boundary, lastAdmitted);
            Assert.True(shiftCells >= 2);
            Assert.Equal(lastAdmitted, carried.Space.GetActor(player).Feet);
            EncounterRetreatContinuation pending = carried.Retreat!.Active!;
            Assert.Equal((active.Command.CommandId, active.SamplesTaken, active.ReadinessCharged),
                (pending.Command.CommandId, pending.SamplesTaken, pending.ReadinessCharged));
            Assert.Equal(active.Intent.Route, pending.Intent.Route);
            // The movement owner still supports the next sample; it lands on the placement refused above for the dry envelope.
            LocalSpatialSnapshot nextSample = WalkSample(carried.Space, player, pending.NextDirection);
            Assert.Equal(refusedSpace.GetActor(player).Feet, nextSample.GetActor(player).Feet);

            WorldSnapshot interrupted = Resolve(carried);
            EncounterRetreatOutcome record = interrupted.Retreat!.Last!;
            Assert.Equal((EncounterRetreatStatus.Interrupted, EncounterRetreatReason.Unadmitted), (record.Status, record.Reason));
            Assert.Equal((pending.Command.CommandId, pending.SamplesTaken, south.Count), (record.CommandId, record.SamplesTaken, record.RouteSamples));
            Assert.Null(interrupted.Retreat.Active);
            Assert.Equal(lastAdmitted, interrupted.Space.GetActor(player).Feet);
            Assert.Equal(lastAdmitted, record.Final);
            // The refused sample is still the actor's spent boundary: exactly one sample's readiness is charged.
            ulong delay = EncounterRetreatRules.SampleDelay(carried.Pacing!.Encounter!.GetActor(player).NormalizedSpeed);
            Assert.Equal(delay, interrupted.Pacing!.Encounter!.GetActor(player).RemainingDelay);
            Assert.Equal(pending.ReadinessCharged + delay, record.ReadinessCharged);
            Assert.Equal(EncounterLifecyclePhase.Active, interrupted.EncounterLifecycle!.Phase);
            Assert.Equal(carried.Progression!.GetActor(player).CurrentHp, interrupted.Progression!.GetActor(player).CurrentHp);
            LocalRetreatView seen = new LocalWorldView(interrupted, player).Retreat!;
            Assert.Equal((EncounterRetreatReason.Unadmitted, pending.Command.CommandId), (seen.Last!.Reason, seen.Last.CommandId));
            Assert.Null(seen.Progress);

            // The same committed walk where the envelope does not lie across it takes its next sample normally.
            WorldSnapshot stepped = Resolve(walking);
            Assert.Equal(active.SamplesTaken + 1, stepped.Retreat!.Active!.SamplesTaken);
            Assert.NotEqual(walking.Space.GetActor(player).Feet, stepped.Space.GetActor(player).Feet);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>The player's first ready boundary against the real first wolf, through ordinary session cutoffs.</summary>
    private static async Task<Boundary> FirstBoundary(string directory)
    {
        WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(Seed, "Traveler");
        await using var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false);
        Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
        LocalRetreatOffer offer = await world.PlayerRetreatOffer();
        Assert.True(offer.CanQueue, $"The real boundary must offer a retreat: {offer.BlockedReason}.");
        var boundary = new Boundary(world.State, world.Player, world.FirstScenario.TargetActorId);
        world.Session.SetReady(false);
        await world.StopAsync();
        return boundary;
    }

    /// <summary>The bank geometry: the player at <paramref name="feet"/> and the real wolf one cell north of it.</summary>
    private static Dictionary<EntityId, SubunitPosition> Placement(Boundary boundary, SubunitPosition feet) =>
        new() { [boundary.Player] = feet, [boundary.Wolf] = North(feet, 1) };

    private static SubunitPosition North(SubunitPosition feet, long cells) =>
        new(feet.X, feet.Y - cells * SubunitPosition.SubunitsPerCell, feet.Z);

    /// <summary>MARKED COUNTERFACTUAL: the same admitted root with only the named actors' feet replaced (revision advanced).</summary>
    private static WorldSnapshot Relocated(WorldSnapshot state, IReadOnlyDictionary<EntityId, SubunitPosition> feet,
        EncounterRetreatState? retreat = null)
    {
        LocalSpatialSnapshot space = state.Space.WithActors(state.Space.Actors.Select(actor => feet.TryGetValue(actor.ActorId, out SubunitPosition at)
            ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, at, actor.Revision + 1, actor.HomePlaceId) : actor));
        return Counterfactual(state, space, retreat);
    }

    /// <summary>The public complete root constructor with every owner but space (and optionally the retreat) retained.</summary>
    private static WorldSnapshot Counterfactual(WorldSnapshot state, LocalSpatialSnapshot space, EncounterRetreatState? retreat = null) =>
        new(state.Inventory, space, state.Residents, state.AdmissionVersion, state.Calendar, state.Revision,
            state.AdvancingSteps, state.Dialogues, state.Gathering, state.Production, state.Progression, state.Quests,
            state.Construction, reservoirs: state.Reservoirs, reservoirSeals: state.ReservoirSeals, liquidCells: state.LiquidCells,
            excavation: state.Excavation, pacing: state.Pacing, encounterLifecycle: state.EncounterLifecycle,
            care: state.Care, resourceHistory: state.ResourceHistory, coordination: state.Coordination, retreat: retreat ?? state.Retreat);

    /// <summary>
    /// The straight southward route from the bank over the divider until contact with every living opponent is clear,
    /// each sample prepared by the ordinary movement owner, and the first prefix the complete root refuses.
    /// </summary>
    private static (List<EncounterRetreatDirection> Route, int FirstRefused) SouthOverDivider(WorldSnapshot world, EntityId player)
    {
        List<EncounterRetreatDirection> route = [];
        int firstRefused = 0;
        LocalSpatialSnapshot space = world.Space;
        while (true)
        {
            Assert.True(route.Count < EncounterRetreatRules.MaximumRouteSamples, "Contact must clear within one bounded route.");
            space = WalkSample(space, player, EncounterRetreatDirection.South);
            route.Add(EncounterRetreatDirection.South);
            if (firstRefused == 0 && !WorldEncounterRetreats.AdmitsPlacement(world, space)) firstRefused = route.Count;
            if (WorldEncounterRetreats.IsClearOfContact(world, space, player)) return (route, firstRefused);
        }
    }

    /// <summary>One sample through the movement owner, which must prepare it.</summary>
    private static LocalSpatialSnapshot WalkSample(LocalSpatialSnapshot space, EntityId player, EncounterRetreatDirection direction)
    {
        LocalSpatialSnapshot? next = WorldEncounterRetreats.WalkSample(space, player, direction, out SpatialMoveStatus status);
        Assert.True(next is not null && status == SpatialMoveStatus.Prepared, $"Movement must support the {direction} sample: {status}.");
        return next!;
    }

    private static LocalSpatialSnapshot Walk(LocalSpatialSnapshot space, EntityId player, IEnumerable<EncounterRetreatDirection> route)
    {
        foreach (EncounterRetreatDirection direction in route) space = WalkSample(space, player, direction);
        return space;
    }

    /// <summary>
    /// MARKED COUNTERFACTUAL commit place: the bank geometry carried whole cells north until the real queue owner admits
    /// the same straight southward route there (it then never reaches the envelope).
    /// </summary>
    private static WorldSnapshot CommitPlace(Boundary boundary, IReadOnlyList<EncounterRetreatDirection> route, out long shiftCells)
    {
        for (shiftCells = 2; shiftCells <= 8; shiftCells++)
        {
            SubunitPosition origin = North(Bank, shiftCells);
            WorldSnapshot candidate;
            try { candidate = Relocated(boundary.State, Placement(boundary, origin)); }
            catch (ArgumentException) { continue; }
            EncounterTimingSnapshot timing = candidate.Pacing!.Encounter!;
            var intent = new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision, QueueEncounterRetreatIntent.Endpoint(origin, route), route);
            if (WorldEncounterRetreats.EvaluateQueue(candidate, boundary.Player, intent) == WorldActionRejection.None) return candidate;
        }
        throw new InvalidOperationException("No whole-cell shift north of the bank admits the straight southward route.");
    }

    /// <summary>
    /// MARKED COUNTERFACTUAL after commitment: the committed walk (command identity, route, samples walked, readiness
    /// charged), the player and the wolf are carried by one rigid translation so the walked feet are
    /// <paramref name="feet"/>. Equivalent to the envelope moving under the committed route, which real content cannot do.
    /// </summary>
    private static WorldSnapshot Carried(WorldSnapshot walking, Boundary boundary, SubunitPosition feet)
    {
        EncounterRetreatState state = walking.Retreat!;
        EncounterRetreatContinuation active = state.Active!;
        long dx = feet.X - active.ExpectedFeet.X;
        long dy = feet.Y - active.ExpectedFeet.Y;
        SubunitPosition Shift(SubunitPosition at) => new(at.X + dx, at.Y + dy, at.Z);
        WorldActionCommand command = active.Command;
        var moved = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId, command.ActorId,
            command.WorldId, command.BranchId, command.ExpectedActorRevision, new QueueEncounterRetreatIntent(active.Intent.EncounterId,
                active.Intent.ExpectedTimingRevision, Shift(active.Intent.Destination), active.Intent.Route));
        var retreat = new EncounterRetreatState(state.RulesPin, state.AcceptedCount,
            new EncounterRetreatContinuation(moved, Shift(active.Origin), active.SamplesTaken, active.ReadinessCharged), state.Last);
        return Relocated(walking, new Dictionary<EntityId, SubunitPosition>
        {
            [boundary.Player] = Shift(walking.Space.GetActor(boundary.Player).Feet),
            [boundary.Wolf] = Shift(walking.Space.GetActor(boundary.Wolf).Feet)
        }, retreat);
    }

    private static WorldActionCommand Command(WorldSnapshot world, EntityId actor, WorldActionIntent intent)
    {
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == actor);
        return new WorldActionCommand(RetreatJourneyWorld.NewId(), control.PrincipalId, control.SessionId, actor, world.WorldId,
            world.BranchId, world.Space.GetActor(actor).Revision, intent);
    }

    private static WorldStepDecision Decide(WorldSnapshot world, WorldBatchKind kind, params WorldActionCommand[] actions) =>
        new WorldStepProcessor().Decide(world, new WorldStepBatch(RetreatJourneyWorld.NewId(), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, kind, [], actions, world.ProfileVersion), _ => false);

    private static WorldSnapshot Resolve(WorldSnapshot world) => Decide(world, WorldBatchKind.EncounterResolution).Candidate;

    /// <summary>Ordinary boundaries (automatic due actors Wait, ticks otherwise) until the actor is the first due actor.</summary>
    private static WorldSnapshot UntilDue(WorldSnapshot current, EntityId actor, Func<WorldSnapshot, bool> stop)
    {
        for (int boundaries = 0; ; boundaries++)
        {
            Assert.True(boundaries < 1_000, "The actor's boundary was never reached.");
            var due = WorldEncounterActions.DueActors(current);
            if (due.Count != 0 && due[0].ActorId == actor && stop(current)) return current;
            EncounterTimingSnapshot timing = current.Pacing!.Encounter!;
            WorldActionCommand[] waits = due.Where(value => value.Control == EncounterActorControl.Automatic
                    && timing.Decisions.All(decision => decision.ActorId != value.ActorId) && !WorldEncounterRetreats.HoldsActor(current, value.ActorId))
                .Select(value => Command(current, value.ActorId, new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision))).ToArray();
            WorldBatchKind kind = waits.Length != 0 ? WorldBatchKind.EncounterDecision
                : due.Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
            current = Decide(current, kind, waits).Candidate;
        }
    }
}
