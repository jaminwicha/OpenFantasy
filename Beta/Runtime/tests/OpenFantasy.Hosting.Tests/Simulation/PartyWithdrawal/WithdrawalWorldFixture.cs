using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

/// <summary>Actual ordinary ground movement and encounter transactions, with original identities and retained replay inputs.</summary>
internal sealed class WithdrawalWorldFixture
{
    private static readonly Lazy<WorldSnapshot> LegacyValue = new(() => AdventureWorldAdmission.CreateEmbedded(
        "withdrawal-world-contract", ["Rowan", "Mira"]));
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Lazy<WithdrawalWorldFixture>> Prepared = new();
    private ulong _identity = 1;
    private readonly HashSet<EntityId> _occupied = [];
    private readonly List<WorldStepBatch> _batches = [];
    internal static WorldSnapshot Legacy => LegacyValue.Value;
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot State { get; private set; }
    internal EntityId[] Humans { get; }
    internal IReadOnlyList<WorldStepBatch> Batches => _batches;

    private WithdrawalWorldFixture(WorldSnapshot genesis)
    {
        Genesis = genesis;
        State = genesis;
        Humans = genesis.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).ToArray();
    }

    internal static WithdrawalWorldFixture Active(int humanCount = 2)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(humanCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(humanCount, 4);
        var basis = Prepared.GetOrAdd(humanCount, count => new(() => Prepare(count))).Value;
        var fixture = new WithdrawalWorldFixture(basis.Genesis) { State = basis.State, _identity = basis._identity };
        fixture._occupied.UnionWith(basis._occupied);
        fixture._batches.AddRange(basis._batches);
        return fixture;
    }

    internal EntityId NextId() => new(0x5749544844524157, _identity++);
    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        var control = State.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }

    internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent)
    {
        var delivery = Delivery(actor);
        return new(NextId(), delivery.PrincipalId, delivery.SessionId, actor, State.WorldId, State.BranchId,
            State.Space.GetActor(actor).Revision, intent);
    }

    internal WorldStepDecision Apply(WorldBatchKind kind, WorldActionCommand[]? actions = null, WorldMovementInput[]? movement = null,
        WorldControllerEvent? controllerEvent = null, WorldControlAssignmentEvent? assignmentEvent = null)
    {
        var batch = new WorldStepBatch(assignmentEvent?.Witness?.AssignmentId ?? NextId(), State.WorldId, State.BranchId,
            State.Revision, State.AdvancingSteps, kind, movement ?? [], actions ?? [], State.ProfileVersion,
            controllerEvent, assignmentEvent);
        var encoded = WorldStepCodec.Encode(batch);
        var decoded = WorldStepCodec.Decode(encoded);
        Assert.Equal(encoded, WorldStepCodec.Encode(decoded));
        var result = new WorldStepProcessor().Decide(State, decoded, _occupied.Contains);
        _occupied.UnionWith(result.Identities.Select(value => value.Identity));
        State = result.Candidate;
        _batches.Add(batch);
        return result;
    }

    internal WorldActionCommand Retreat(EntityId actor)
    {
        var advice = WorldEncounterRetreatAdvisor.Advise(State, actor);
        Assert.True(advice.IsFound, $"{actor} requires a real clear route: {advice.Status}");
        return Command(actor, new QueueEncounterRetreatIntent(State.Pacing!.Encounter!.EncounterId,
            State.Pacing.Encounter.Revision, advice.Destination, advice.Route));
    }

    internal WorldActionCommand Wait(EntityId actor) => Command(actor,
        new QueueEncounterWaitIntent(State.Pacing!.Encounter!.EncounterId, State.Pacing.Encounter.Revision));

    internal void Choose(params WorldActionCommand[] commands)
    {
        foreach (var receipt in Apply(WorldBatchKind.EncounterDecision, commands).Receipts)
            Assert.True(receipt.Status == CommandReceiptStatus.Applied, $"Unexpected refusal: {receipt.Rejection}");
    }

    internal void ChooseDue(bool attacks = true)
    {
        var state = State;
        List<WorldActionCommand> commands = [];
        foreach (var actor in WorldEncounterActions.DueActors(state))
        {
            if (state.Pacing!.Encounter!.Decisions.Any(choice => choice.ActorId == actor.ActorId)
                || WorldEncounterRetreats.HoldsActor(state, actor.ActorId)) continue;
            QueueEncounterAbilityIntent? attack = null;
            if (attacks && !Humans.Contains(actor.ActorId))
            {
                attack = (from ability in state.EncounterActions!.GetCombatant(actor.ActorId).AbilityIds
                    from human in Humans
                    let intent = new QueueEncounterAbilityIntent(state.Pacing.Encounter.EncounterId,
                        state.Pacing.Encounter.Revision, ability, human)
                    where WorldEncounterActions.EvaluateAbility(state, actor.ActorId, intent) == WorldActionRejection.None
                    select intent).FirstOrDefault();
            }
            commands.Add(attack is null ? Wait(actor.ActorId) : Command(actor.ActorId, attack));
        }
        if (commands.Count != 0) Choose(commands.ToArray());
    }

    internal void ResolveOrTick() => Apply(WorldEncounterActions.DueActors(State).Count == 0
        ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution);

    internal void Restore()
    {
        byte[] bytes = WorldSnapshotCodec.Encode(State);
        State = State.Coordination!.Anchor is { } anchor ? WorldSnapshotCodec.DecodeCoordinated(bytes, anchor) : WorldSnapshotCodec.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(State));
    }

    internal void ControllerEvent(EntityId actor, WorldControllerEventKind kind)
    {
        var row = State.Controllers!.Get(actor);
        var input = kind == WorldControllerEventKind.Disconnected
            ? new WorldControllerEvent(actor, kind, row.Revision, reason: WorldControllerDisconnectReason.TransportLost)
            : new WorldControllerEvent(actor, kind, row.Revision, row.DisconnectId,
                kind == WorldControllerEventKind.AllowanceExpired ? 30_000UL : 0);
        Apply(WorldBatchKind.ControllerLifecycle, controllerEvent: input);
    }

    private static WithdrawalWorldFixture Prepare(int humanCount)
    {
        string[] names = ["Rowan", "Mira", "Aster", "Fern"];
        var legacy = humanCount == 2 ? Legacy : AdventureWorldAdmission.CreateEmbedded(
            "withdrawal-world-contract", names.Take(humanCount).ToArray());
        var fixture = new WithdrawalWorldFixture(WorldPartyWithdrawalAdmission.CreateFresh(legacy));
        var scenario = fixture.State.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        EntityId lead = scenario.InitiatingActorId;
        var target = fixture.State.Space.GetActor(scenario.TargetActorId).Feet;
        bool InReach(EntityId actor) => actor == lead || fixture.State.Space.QueryInteraction(lead, actor,
            scenario.EntryRangeSubunits) == SpatialInteraction.Reachable;
        void Walk(EntityId actor, SubunitPosition destination, Func<bool> arrived)
        {
            for (int step = 0; !arrived(); step++)
            {
                Assert.True(step < 512, "Assembly must use bounded ordinary movement.");
                var route = fixture.State.Space.QueryGroundRoute(actor, destination);
                Assert.Equal(GroundRouteStatus.Reachable, route.Status);
                var feet = fixture.State.Space.GetActor(actor).Feet;
                var next = route.NextWaypoint!.Value;
                long dx = Math.Clamp(next.X - feet.X, -32, 32);
                long dy = dx != 0 ? 0 : Math.Clamp(next.Y - feet.Y, -32, 32);
                fixture.Apply(WorldBatchKind.AdvancingStep, movement: [new(fixture.Delivery(actor), actor, dx, dy)]);
                Assert.Equal(feet.Offset(dx, dy, 0), fixture.State.Space.GetActor(actor).Feet);
            }
        }
        var approach = new[] { target.Offset(256, 0, 0), target.Offset(-256, 0, 0), target.Offset(0, 256, 0), target.Offset(0, -256, 0) }
            .Select(feet => (Feet: feet, Route: fixture.State.Space.QueryGroundRoute(lead, feet)))
            .Where(value => value.Route.Status == GroundRouteStatus.Reachable).OrderBy(value => value.Route.Waypoints.Count).First().Feet;
        Walk(lead, approach, () => InReach(scenario.TargetActorId));
        var center = fixture.State.Space.GetActor(lead).Feet;
        foreach (var human in fixture.Humans.Where(human => !InReach(human)))
        {
            var destination = (from x in new[] { -256L, 0L, 256L } from y in new[] { -256L, 0L, 256L }
                where x != 0 || y != 0 let feet = center.Offset(x, y, 0)
                let route = fixture.State.Space.QueryGroundRoute(human, feet)
                where route.Status == GroundRouteStatus.Reachable orderby route.Waypoints.Count select feet).First();
            Walk(human, destination, () => fixture.State.Space.GetActor(human).Feet == destination);
        }
        Assert.All(scenario.Combatants.Where(actor => actor.Side != 0), actor => Assert.True(InReach(actor.ActorId)));
        var entry = fixture.Command(lead, new EnterEncounterIntent(scenario.DefinitionId,
            EncounterEpisodeIdentity.Derive(fixture.State.EncounterLifecycle!, scenario.DefinitionId),
            fixture.State.EncounterLifecycle!.EncounterCount, fixture.State.Space.GetActor(scenario.TargetActorId).Revision));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(fixture.Apply(WorldBatchKind.EncounterLifecycle, [entry]).Receipts).Status);
        fixture.Apply(WorldBatchKind.EncounterLifecycle);
        fixture.Apply(WorldBatchKind.EncounterLifecycle);
        Assert.Equal(EncounterLifecyclePhase.Active, fixture.State.EncounterLifecycle!.Phase);
        return fixture;
    }
}
