using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using static OpenFantasy.Core.Tests.Gathering.GatheringFixture;

namespace OpenFantasy.Core.Tests.Gathering;

public sealed class GatheringTests
{
    [Fact]
    public void SwingIssuesOnlyTheNextAdmittedIntervalAndPreservesTheOriginal()
    {
        WorldSnapshot world = GatheringFixture.Create(power: 3);
        GatherPlan result = Plan(world);
        Assert.Equal(WorldActionRejection.None, result.Rejection);
        EntityId spawned = EntityId.DeriveSpawned(Command(world).CommandId, 0);
        Assert.Equal(spawned, result.SpawnedId);
        CountedSourceLot lot = result.CandidateInventory.Custody.Lots.Single(value => value.LotId == spawned);
        Assert.Equal(new SourceInterval(Source, 0, 3), lot.Source);
        Assert.Equal(Material, lot.Definition);
        Assert.Equal(Id(60), lot.CustodianId);
        Assert.Equal(600U, result.CandidateGathering.GetActorWork(Id(10)).Effort);
        Assert.Equal(4, result.CandidateGathering.GetNode(Node).RemainingSwings);
        Assert.Equal(3UL, result.CandidateGathering.GetNode(Node).NextUnissuedUnit);
        Assert.Equal(1UL, result.CandidateInventory.Custody.Revision);
        Assert.Equal(1UL, result.CandidateInventory.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(0UL, result.CandidateInventory.Custody.GetStorage(Id(61)).Revision);
        Assert.Equal(1000U, world.Gathering!.GetActorWork(Id(10)).Effort);
        Assert.Equal(0UL, world.Gathering.GetNode(Node).NextUnissuedUnit);
        Assert.Equal(2, world.Inventory.Custody.Lots.Count);
        _ = With(world, result.CandidateInventory, result.CandidateGathering);
    }

    [Fact]
    public void BankedEffortAllowsTwoSwingsAndDoesNotInventCooldownTime()
    {
        WorldSnapshot world = GatheringFixture.Create();
        GatherPlan first = Plan(world);
        GatherPlan second = new GatherPlanner().Plan(world, first.CandidateInventory, first.CandidateGathering,
            Command(world, 301, intent: new GatherIntent(Node, 1, ToolLot, 0)));
        Assert.Equal(WorldActionRejection.None, second.Rejection);
        Assert.Equal(200U, second.CandidateGathering.GetActorWork(Id(10)).Effort);
        Assert.Equal(0UL, second.CandidateGathering.AdvancingStep);
        GatherPlan third = new GatherPlanner().Plan(world, second.CandidateInventory, second.CandidateGathering,
            Command(world, 302, intent: new GatherIntent(Node, 2, ToolLot, 0)));
        Assert.Equal(WorldActionRejection.Unavailable, third.Rejection);
        Assert.Same(second.CandidateInventory, third.CandidateInventory);
        Assert.Same(second.CandidateGathering, third.CandidateGathering);
        GatheringSnapshot advanced = second.CandidateGathering;
        for (ulong step = 1; step <= 20; step++) { advanced = advanced.AdvanceStep(step); }
        Assert.Equal(400U, advanced.GetActorWork(Id(10)).Effort);
        Assert.Equal(1000U, advanced.GetActorWork(Id(11)).Effort);
    }

    [Fact]
    public void EffortStartsAtZeroAndOnlyExactAdvancingStepsRechargeIt()
    {
        GatheringSnapshot original = GatheringFixture.Create(effort: 0).Gathering!;
        Assert.Throws<ArgumentException>(() => original.AdvanceStep(0));
        Assert.Throws<ArgumentException>(() => original.AdvanceStep(2));
        GatheringSnapshot advanced = original;
        for (ulong step = 1; step <= 150; step++) { advanced = advanced.AdvanceStep(step); }
        Assert.All(advanced.Actors, actor => Assert.Equal(1000U, actor.Effort));
        Assert.All(original.Actors, actor => Assert.Equal(0U, actor.Effort));
        Assert.Equal(WorldActionRejection.Unavailable, Plan(GatheringFixture.Create(effort: 399)).Rejection);
        Assert.Equal(WorldActionRejection.None, Plan(GatheringFixture.Create(effort: 400)).Rejection);
    }

    [Fact]
    public void ExactPowerNeverCapsYieldAtTheFiniteSourceBoundary()
    {
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(power: 1, sourceCapacity: 4));
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(power: 3, sourceCapacity: 5, cursor: 3, swings: 4));
        WorldSnapshot world = GatheringFixture.Create(power: 3, sourceCapacity: 15);
        Assert.Equal(3UL, Plan(world).CandidateGathering.GetNode(Node).NextUnissuedUnit);
    }

    [Fact]
    public void DepletionUsesTheActionStartStepAndRegrowthNeverResetsTheCursor()
    {
        WorldSnapshot world = GatheringFixture.Create(cursor: 4, swings: 1, step: 10);
        GatherPlan result = Plan(world);
        GatherNodeState node = result.CandidateGathering.GetNode(Node);
        Assert.Equal(10UL, node.DepletedAtStep);
        Assert.Equal(910UL, node.RespawnAtAdvancingStep);
        Assert.Equal(GatherNodeVisibility.Stump, node.VisibilityAt(29));
        Assert.Equal(GatherNodeVisibility.Absent, node.VisibilityAt(30));
        Assert.Throws<ArgumentOutOfRangeException>(() => node.VisibilityAt(9));
        GatheringSnapshot state = result.CandidateGathering;
        for (ulong step = 11; step < 910; step++) { state = state.AdvanceStep(step); }
        Assert.Equal(0, state.GetNode(Node).RemainingSwings);
        state = state.AdvanceStep(910);
        Assert.Equal(5, state.GetNode(Node).RemainingSwings);
        Assert.Equal(5UL, state.GetNode(Node).NextUnissuedUnit);
        Assert.Equal(2UL, state.GetNode(Node).Revision);
        Assert.Null(state.GetNode(Node).DepletedAtStep);
    }

    [Fact]
    public void RegrowthRequiresACompleteCycleAtTheStrongestAdmittedToolPower()
    {
        WorldSnapshot world = GatheringFixture.Create(sourceCapacity: 19, cursor: 5, swings: 0, step: 899, depletedAt: 0);
        GatheringSnapshot original = world.Gathering!;
        var stronger = new GatherToolDefinition(new CountedItemDefinition(Id(209), OpenFantasy.Core.Tests.Worlds.AdmissionFixture.Version), GatherToolKind.Axe, 3);
        var snapshot = new GatheringSnapshot(world.WorldId, world.BranchId, 899, original.Nodes,
            [.. original.Tools, stronger], original.Actors);
        Assert.Equal(0, snapshot.AdvanceStep(900).GetNode(Node).RemainingSwings);
        Assert.Equal(5, original.AdvanceStep(900).GetNode(Node).RemainingSwings);
    }

    [Fact]
    public void BerryKeepsItsOwnFourSwingAndSevenHundredStepDefinition()
    {
        GatheringSnapshot original = GatheringFixture.Create().Gathering!;
        var definition = new GatherNodeDefinition(Id(212), GatherNodeKind.Berry, Material);
        var node = new GatherNodeState(Node, definition, Source, 20, original.Nodes[0].Feet, 4, 4, 0, 0);
        var state = new GatheringSnapshot(original.WorldId, original.BranchId, 699, [node],
            [new(Axe, GatherToolKind.Sickle, 1)], original.Actors);
        Assert.Equal(4, state.AdvanceStep(700).GetNode(Node).RemainingSwings);
        Assert.Equal(GatherToolKind.Sickle, definition.RequiredToolKind);
    }

    [Fact]
    public void CurrentControlIsCheckedBeforePrivateLotOrRevisionDetails()
    {
        WorldSnapshot world = GatheringFixture.Create();
        WorldActionCommand valid = Command(world);
        var forged = new WorldActionCommand(valid.CommandId, Id(900), valid.OriginSessionId, valid.ActorId,
            valid.WorldId, valid.BranchId, ulong.MaxValue, new GatherIntent(Node, ulong.MaxValue, Id(901), ulong.MaxValue));
        Assert.Equal(WorldActionRejection.NotAuthorized, Plan(world, forged).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, Plan(world,
            Command(world, intent: new GatherIntent(Node, 0, Id(901), 0))).Rejection);
        WorldSnapshot foreignTool = GatheringFixture.Create(toolOwner: Id(61));
        Assert.Equal(WorldActionRejection.Unavailable, Plan(foreignTool).Rejection);
    }

    [Fact]
    public void ToolsRequireExactPinsCorrectKindCurrentRevisionAndNoReservation()
    {
        Assert.Equal(WorldActionRejection.Unavailable, Plan(GatheringFixture.Create(protection: LotProtection.UserReserved)).Rejection);
        WorldSnapshot world = GatheringFixture.Create();
        Assert.Equal(WorldActionRejection.Unavailable,
            Plan(world, Command(world, intent: new GatherIntent(Node, 0, ToolLot, 1))).Rejection);
        GatheringSnapshot old = world.Gathering!;
        var wrongPin = new GatherToolDefinition(new CountedItemDefinition(Axe.DefinitionId,
            ContentHash.Compute("different-tool"u8)), GatherToolKind.Axe, 1);
        Assert.Throws<ArgumentException>(() => With(world, gathering: new GatheringSnapshot(old.WorldId, old.BranchId,
            old.AdvancingStep, old.Nodes, [wrongPin], old.Actors)));
    }

    [Fact]
    public void BodyFunctionLossAndReadingHoldRejectWithoutSpendingEffort()
    {
        WorldSnapshot world = GatheringFixture.Create();
        ActorPresence actor = world.Space.GetActor(Id(10));
        var injured = new ActorPresence(actor.Binding, new BodyConditionSnapshot(actor.Binding.FormInstanceId,
            actor.Condition.Definition, [new(Id(401), Id(402), new BodySlotKey("core"),
                BodyInjuryKind.FunctionLoss, BodyFunction.Manipulation)]), actor.Scope, actor.Feet, actor.Revision, actor.HomePlaceId);
        var space = new LocalSpatialSnapshot(world.Space.Scope, world.Space.Regions,
            world.Space.Actors.Select(value => value.ActorId == injured.ActorId ? injured : value));
        Assert.Equal(WorldActionRejection.Unavailable, Plan(With(world, space: space)).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, Plan(With(world, dialogues: [Dialogue()])).Rejection);
    }

    [Fact]
    public void CapacityAndRevisionOverflowRejectWithoutChangingEitherCandidate()
    {
        foreach (WorldSnapshot world in new[] { GatheringFixture.Create(storageCapacity: 7),
            GatheringFixture.Create(nodeRevision: ulong.MaxValue) })
        {
            GatherPlan result = Plan(world);
            Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
            Assert.Same(world.Inventory, result.CandidateInventory);
            Assert.Same(world.Gathering, result.CandidateGathering);
        }
    }

    [Fact]
    public void AdmissionRejectsIssuedClaimsBeyondCursorAndIncompleteActorWork()
    {
        WorldSnapshot world = GatheringFixture.Create();
        GatheringSnapshot old = world.Gathering!;
        Assert.Throws<ArgumentException>(() => With(world, gathering: new GatheringSnapshot(old.WorldId, old.BranchId,
            0, old.Nodes, old.Tools, [old.Actors[0]])));
        CountedSourceLot issued = new(Id(403), Id(60), Material, new SourceInterval(Source, 0, 1), 0);
        var custody = new CustodySnapshot(world.WorldId, world.BranchId, 0, world.Inventory.Custody.Storages,
            [.. world.Inventory.Custody.Lots, issued]);
        var inventory = new CountedAuthorityState(custody, world.Inventory.AdmittedUnitCapacity,
            world.Inventory.Actors, world.Inventory.Controls, world.Inventory.Policies, world.Inventory.Sources);
        Assert.Throws<ArgumentException>(() => With(world, inventory));
    }

    [Fact]
    public void CanonicalCollectionsCopySortAndRejectDuplicatesConflictingPinsAndIdentityAliases()
    {
        GatheringSnapshot original = GatheringFixture.Create().Gathering!;
        GatherActorWork[] actors = original.Actors.Reverse().ToArray();
        var state = new GatheringSnapshot(original.WorldId, original.BranchId, 0, original.Nodes, original.Tools, actors);
        actors[0] = new GatherActorWork(Id(999), 1);
        Assert.Equal(original.ContentHash, state.ContentHash);
        Assert.Throws<ArgumentException>(() => new GatheringSnapshot(original.WorldId, original.BranchId, 0,
            [original.Nodes[0], original.Nodes[0]], original.Tools, original.Actors));
        Assert.Throws<ArgumentException>(() => new GatheringSnapshot(original.WorldId, original.BranchId, 0,
            original.Nodes, original.Tools, [new GatherActorWork(Node, 1)]));
        var conflicting = new GatherToolDefinition(new CountedItemDefinition(Material.DefinitionId,
            ContentHash.Compute("wrong"u8)), GatherToolKind.Axe, 1);
        Assert.Throws<ArgumentException>(() => new GatheringSnapshot(original.WorldId, original.BranchId, 0,
            original.Nodes, [conflicting], original.Actors));
    }

    [Fact]
    public void InvalidScalarStatesAndFutureOrOverflowingDepletionAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GatherActorWork(Id(10), 1001));
        Assert.Throws<ArgumentException>(() => new GatherToolDefinition(Axe, GatherToolKind.Axe, 0));
        Assert.Throws<ArgumentException>(() => new GatherToolDefinition(Axe, GatherToolKind.Axe, 4));
        Assert.Throws<ArgumentException>(() => new GatherNodeState(Node, Oak, Source, 5, default, 0, 0, 4));
        Assert.Throws<ArgumentException>(() => new GatherNodeState(Node, Oak, Source, 5, default, 0, 6, 5));
        Assert.Throws<OverflowException>(() => new GatherNodeState(Node, Oak, Source, 5, default, 5, 5, 0, ulong.MaxValue));
        Assert.Throws<ArgumentException>(() => GatheringFixture.Create(cursor: 5, swings: 0, depletedAt: 1));
        GatheringSnapshot old = GatheringFixture.Create().Gathering!;
        var terminal = new GatheringSnapshot(old.WorldId, old.BranchId, ulong.MaxValue, old.Nodes, old.Tools, old.Actors);
        Assert.Throws<OverflowException>(() => terminal.AdvanceStep(0));
        Assert.Equal(ulong.MaxValue, terminal.AdvancingStep);
    }
    [Fact]
    public void AFullLotTableRejectsWithoutThrowingOrSpendingTheSource()
    {
        WorldSnapshot world = GatheringFixture.Create(storageCapacity: 8192);
        const ulong fillerCount = CustodySnapshot.MaximumLots - 2;
        CountedSourceLot[] filler = Enumerable.Range(0, (int)fillerCount).Select(index =>
            new CountedSourceLot(Id((ulong)index + 10000), Id(60), Material,
                new SourceInterval(Id(410), (ulong)index, (ulong)index + 1), 0)).ToArray();
        CountedAuthorityState old = world.Inventory;
        var custody = new CustodySnapshot(world.WorldId, world.BranchId, 0, old.Custody.Storages,
            [.. old.Custody.Lots, .. filler]);
        var inventory = new CountedAuthorityState(custody, old.AdmittedUnitCapacity + fillerCount, old.Actors,
            old.Controls, old.Policies, [.. old.Sources, new(Id(410), Material, fillerCount)]);
        WorldSnapshot full = With(world, inventory);
        GatherPlan result = Plan(full);
        Assert.Equal(WorldActionRejection.Unavailable, result.Rejection);
        Assert.Same(full.Inventory, result.CandidateInventory);
        Assert.Equal(0UL, result.CandidateGathering.GetNode(Node).NextUnissuedUnit);
    }

    [Fact]
    public void InconsistentSequentialCandidatesCannotReissueAnExistingSourceInterval()
    {
        WorldSnapshot world = GatheringFixture.Create();
        GatherPlan first = Plan(world);
        GatherPlan second = new GatherPlanner().Plan(world, first.CandidateInventory, world.Gathering!, Command(world, 301));
        Assert.Equal(WorldActionRejection.Unavailable, second.Rejection);
        Assert.Same(first.CandidateInventory, second.CandidateInventory);
        Assert.Same(world.Gathering, second.CandidateGathering);
    }

    [Fact]
    public void BoundedEnumerationsRejectBeforeConsumingAnUnboundedSequence()
    {
        GatheringSnapshot original = GatheringFixture.Create().Gathering!;
        int visited = 0;
        IEnumerable<GatherActorWork> TooManyActors()
        {
            for (ulong index = 0; index < 1000; index++)
            {
                visited++;
                yield return new GatherActorWork(Id(1000 + index), 0);
            }
        }
        Assert.Throws<ArgumentException>(() => new GatheringSnapshot(original.WorldId, original.BranchId, 0,
            original.Nodes, original.Tools, TooManyActors()));
        Assert.Equal(GatheringSnapshot.MaximumActors + 1, visited);
        GatherNodeState[] tooManyNodes = Enumerable.Range(0, GatheringSnapshot.MaximumNodes + 1)
            .Select(index => new GatherNodeState(Id((ulong)index + 1000), Oak, Id((ulong)index + 2000),
                20, original.Nodes[0].Feet, 0, 0, 5)).ToArray();
        Assert.Throws<ArgumentException>(() => new GatheringSnapshot(original.WorldId, original.BranchId, 0,
            tooManyNodes, original.Tools, original.Actors));
        GatherToolDefinition[] tooManyTools = Enumerable.Range(0, GatheringSnapshot.MaximumTools + 1)
            .Select(index => new GatherToolDefinition(new CountedItemDefinition(Id((ulong)index + 1000),
                OpenFantasy.Core.Tests.Worlds.AdmissionFixture.Version), GatherToolKind.Axe, 1)).ToArray();
        Assert.Throws<ArgumentException>(() => new GatheringSnapshot(original.WorldId, original.BranchId, 0,
            original.Nodes, tooManyTools, original.Actors));
    }

}
