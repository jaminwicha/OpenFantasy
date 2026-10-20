using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Simulation.Cognition;

/// <summary>
/// Wren's delayed-supply episode on a fresh profile-42 world created from the unplayed adventure genesis with its
/// original pins. Every effect goes through ordinary commands and the step processor; the fixture's decision helper
/// plays the host's role by running the policy on the actor's own facts, and replay never calls it.
/// </summary>
internal sealed class CognitionWorldFixture
{
    internal const ulong PersonalStockTarget = 2;
    private static readonly Lazy<WorldSnapshot> GenesisValue = new(() => Create(["Rowan"]));
    private readonly HashSet<EntityId> _occupied = [];
    private readonly List<byte[]> _batches = [];
    private ulong _identity = 1;

    internal CognitionWorldFixture() : this(GenesisValue.Value, "Rowan") { }

    /// <summary>The same adventure for another roster; the first player listed is <see cref="Player"/>.</summary>
    internal CognitionWorldFixture(IReadOnlyList<string> players, string seed = DefaultSeed)
        : this(Create(players, seed), players[0]) { }

    private const string DefaultSeed = "cognition-world-contract";

    private static WorldSnapshot Create(IReadOnlyList<string> players, string seed = DefaultSeed) => WorldCognitionAdmission.CreateFresh(
        WorldPartyWithdrawalAdmission.CreateFresh(AdventureWorldAdmission.CreateEmbedded(seed, players)));

    private CognitionWorldFixture(WorldSnapshot genesis, string playerName)
    {
        Genesis = genesis;
        State = Genesis;
        Player = Genesis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player
            && resident.DisplayName == playerName).ActorId;
        ConstructionProject project = Genesis.Construction!.Projects.Single();
        Wren = project.BuilderActorId;
        ProjectId = project.ProjectId;
        Quest = Genesis.Quests!.Definitions.Single();
        Oak = Genesis.Gathering!.Nodes.Single(node => node.Definition.RequiredToolKind == GatherToolKind.Axe).NodeId;
        Axe = Genesis.Gathering.Tools.Single(tool => tool.Kind == GatherToolKind.Axe).Definition;
    }

    internal static WorldSnapshot FreshGenesis => GenesisValue.Value;
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot State { get; private set; }
    internal EntityId Player { get; }
    internal EntityId Wren { get; }
    internal EntityId ProjectId { get; }
    internal EntityId Oak { get; }
    internal DeliveryQuestDefinition Quest { get; }
    internal CountedItemDefinition Axe { get; }
    internal IReadOnlyList<byte[]> Batches => _batches;
    internal ActorCognition CognitionOf(EntityId actor) => State.Cognition!.For(actor);

    internal EntityId NextId() => new(0x434F474E4954494E, _identity++);

    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        ActorControlBinding control = State.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(control.PrincipalId, control.SessionId);
    }

    internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent)
    {
        TrustedCommandDelivery delivery = Delivery(actor);
        return new(NextId(), delivery.PrincipalId, delivery.SessionId, actor, State.WorldId, State.BranchId,
            State.Space.GetActor(actor).Revision, intent);
    }

    internal WorldStepDecision Apply(WorldBatchKind kind, WorldActionCommand[]? actions = null, WorldMovementInput[]? movement = null)
    {
        var batch = new WorldStepBatch(NextId(), State.WorldId, State.BranchId, State.Revision, State.AdvancingSteps,
            kind, movement ?? [], actions ?? [], State.ProfileVersion);
        byte[] encoded = WorldStepCodec.Encode(batch);
        WorldStepBatch decoded = WorldStepCodec.Decode(encoded);
        Assert.Equal(encoded, WorldStepCodec.Encode(decoded));
        WorldStepDecision result = new WorldStepProcessor().Decide(State, decoded, _occupied.Contains);
        _occupied.UnionWith(result.Identities.Select(value => value.Identity));
        State = result.Candidate;
        _batches.Add(encoded);
        return result;
    }

    /// <summary>One boundary of the current kind: an advancing step outside a hold, otherwise a held interaction.</summary>
    internal WorldStepDecision Step(params WorldActionCommand[] actions) =>
        Apply(State.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep, actions);

    internal WorldActionReceipt Act(EntityId actor, WorldActionIntent intent)
    {
        WorldActionCommand command = Command(actor, intent);
        return Step(command).Receipts.Single(receipt => receipt.CommandId == command.CommandId);
    }

    internal void Restore()
    {
        byte[] bytes = WorldSnapshotCodec.Encode(State);
        State = WorldSnapshotCodec.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(State));
    }

    internal CountedSourceLot Owned(EntityId actor, CountedItemDefinition definition) =>
        State.Inventory.Custody.Lots.Single(lot => lot.CustodianId == State.GetResident(actor).InventoryCustodianId
            && lot.Definition == definition);

    internal ulong Holding(EntityId actor, CountedItemDefinition definition) =>
        State.Inventory.Custody.Lots.Where(lot => lot.CustodianId == State.GetResident(actor).InventoryCustodianId
            && lot.Definition == definition).Aggregate(0UL, (total, lot) => checked(total + lot.Quantity));

    internal GiveItemIntent GiftOf(EntityId from, EntityId to, CountedSourceLot lot, ulong quantity)
    {
        EntityId bag = State.GetResident(from).InventoryCustodianId;
        return new GiveItemIntent(to, State.Space.GetActor(to).Revision, lot.LotId, lot.Revision, bag,
            State.Inventory.Custody.GetStorage(bag).Revision, quantity);
    }

    internal WorldActionReceipt GiveAxeToWren() => Act(Player, GiftOf(Player, Wren, Owned(Player, Axe), 1));

    internal InspectGatherSourceIntent InspectionOf(EntityId node) =>
        new(node, State.Gathering!.Nodes.Single(value => value.NodeId == node).Revision);

    internal GatherIntent GatherOf(EntityId actor, EntityId node)
    {
        CountedSourceLot tool = Owned(actor, Axe);
        return new GatherIntent(node, State.Gathering!.Nodes.Single(value => value.NodeId == node).Revision, tool.LotId,
            tool.Revision);
    }

    /// <summary>Talks to Wren, accepts the funded delivery and delivers the original six wood, leaving the hold open.</summary>
    internal WorldActionReceipt DeliverToWren()
    {
        Assert.Equal(CommandReceiptStatus.Applied, Act(Player, new SpeakToActorIntent(Wren, State.Space.GetActor(Wren).Revision)).Status);
        Assert.Equal(CommandReceiptStatus.Applied, Act(Player, new AcceptDeliveryQuestIntent(Quest.DefinitionId, Quest.Version,
            Wren, State.Space.GetActor(Wren).Revision, 0, State.Progression!.GetActor(Player).Revision)).Status);
        EntityId bag = State.GetResident(Player).InventoryCustodianId;
        CountedSourceLot wood = Owned(Player, Quest.Item);
        return Act(Player, new CompleteDeliveryQuestIntent(Quest.DefinitionId, Quest.Version, Wren,
            State.Space.GetActor(Wren).Revision, 1, State.Progression!.GetActor(Player).Revision,
            State.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, Quest.Quantity)]));
    }

    internal WorldActionReceipt CloseDialogue() =>
        Act(Player, new CloseDialogueIntent(State.Dialogues.Single(dialogue => dialogue.ListenerActorId == Player).DialogueId));

    /// <summary>
    /// The host's role: run the policy on the actor's own facts and propose a decision only when it changes
    /// something. Known sources come from the actor's beliefs; the route check only establishes physical feasibility.
    /// </summary>
    internal WorldActionCommand? Decision(EntityId actor, string? policyVersion = null)
    {
        ActorCognition cognition = CognitionOf(actor);
        if (cognition.Wake is not { Suppression: WakeSuppression.None } wake || wake.DueStep > State.AdvancingSteps
            || State.HasReadingHold)
            return null;
        ConstructionProject project = State.Construction!.Projects.Single(value => value.ProjectId == ProjectId);
        var reachable = cognition.Beliefs.Sources.Where(source => StandingNear(actor, source.SourceId) is not null)
            .Select(source => source.SourceId).ToHashSet();
        var situation = new CognitionSituation(State.AdvancingSteps, project.ProjectId, project.QuestId,
            !project.IsCompleted, Holding(actor, Axe) != 0, Holding(actor, Quest.Item), PersonalStockTarget,
            State.Gathering!.GetActorWork(actor).Effort >= GatherRulesV1.EffortPerSwing, State.HasReadingHold, reachable);
        CognitionDecision decided = CognitionPolicy.Decide(cognition, situation, State.Calendar);
        if (policyVersion is not null) decided = decided with { PolicyVersion = policyVersion };
        return ReferenceEquals(cognition.Adopt(decided, State.Calendar), cognition) ? null
            : Command(actor, new RecordCognitionDecisionIntent(cognition.Revision, decided));
    }

    /// <summary>A standing cell beside the source that the actor can reach by ordinary ground movement.</summary>
    internal SubunitPosition? StandingNear(EntityId actor, EntityId node)
    {
        SubunitPosition feet = State.Gathering!.Nodes.Single(value => value.NodeId == node).Feet;
        foreach (SubunitPosition candidate in new[] { feet.Offset(0, -256, 0), feet.Offset(-256, 0, 0), feet.Offset(256, 0, 0),
            feet.Offset(0, 256, 0) })
        {
            if (State.Space.GetActor(actor).Feet == candidate
                || State.Space.QueryGroundRoute(actor, candidate).Status == GroundRouteStatus.Reachable)
                return candidate;
        }
        return null;
    }

    internal void Walk(EntityId actor, SubunitPosition destination, int maximumSteps = 256)
    {
        for (int step = 0; State.Space.GetActor(actor).Feet != destination; step++)
        {
            Assert.True(step < maximumSteps, "The walk must use bounded ordinary movement.");
            GroundRouteResult route = State.Space.QueryGroundRoute(actor, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            SubunitPosition feet = State.Space.GetActor(actor).Feet;
            SubunitPosition next = route.NextWaypoint!.Value;
            long dx = Math.Clamp(next.X - feet.X, -32, 32);
            long dy = dx != 0 ? 0 : Math.Clamp(next.Y - feet.Y, -32, 32);
            Apply(WorldBatchKind.AdvancingStep, movement: [new(Delivery(actor), actor, dx, dy)]);
            Assert.Equal(feet.Offset(dx, dy, 0), State.Space.GetActor(actor).Feet);
        }
    }

    /// <summary>
    /// A long ordinary walk in stages: each stage routes to the reachable cell within six cells that is nearest the
    /// destination, so every sample still comes from the bounded route search.
    /// </summary>
    internal void WalkFar(EntityId actor, SubunitPosition destination, int maximumStages = 64)
    {
        for (int stage = 0; State.Space.GetActor(actor).Feet != destination; stage++)
        {
            Assert.True(stage < maximumStages, "The long walk must make bounded progress.");
            if (State.Space.QueryGroundRoute(actor, destination).Status == GroundRouteStatus.Reachable)
            {
                Walk(actor, destination);
                return;
            }
            SubunitPosition feet = State.Space.GetActor(actor).Feet;
            static Int128 Distance(SubunitPosition a, SubunitPosition b) =>
                ((Int128)a.X - b.X) * ((Int128)a.X - b.X) + ((Int128)a.Y - b.Y) * ((Int128)a.Y - b.Y);
            SubunitPosition next = (from dx in Enumerable.Range(-6, 13) from dy in Enumerable.Range(-6, 13)
                    let candidate = feet.Offset(dx * 256L, dy * 256L, 0)
                    where Distance(candidate, destination) < Distance(feet, destination)
                    orderby Distance(candidate, destination)
                    select candidate)
                .First(candidate => State.Space.QueryGroundRoute(actor, candidate).Status == GroundRouteStatus.Reachable);
            Walk(actor, next);
        }
    }

    internal void AdvanceUntilDue(EntityId actor)
    {
        for (int step = 0; CognitionOf(actor).Wake is { } wake && wake.DueStep > State.AdvancingSteps; step++)
        {
            Assert.True(step <= (int)CognitionPolicy.FallbackSteps, "A pending wake falls due within the fallback cadence.");
            Apply(WorldBatchKind.AdvancingStep);
        }
    }

    /// <summary>Ordinary advancing steps until the actor has earned effort for one swing.</summary>
    internal void RecoverEffort(EntityId actor)
    {
        for (int wait = 0; State.Gathering!.GetActorWork(actor).Effort < GatherRulesV1.EffortPerSwing; wait++)
        {
            Assert.True(wait < 400, "Effort recovers through ordinary advancing steps.");
            Apply(WorldBatchKind.AdvancingStep);
        }
    }

    internal WorldActionReceipt Decide(EntityId actor)
    {
        AdvanceUntilDue(actor);
        WorldActionCommand command = Decision(actor) ?? throw new InvalidOperationException("The policy changed nothing.");
        return Step(command).Receipts.Single(receipt => receipt.CommandId == command.CommandId);
    }
}
