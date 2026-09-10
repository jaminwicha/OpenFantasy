using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal static class GatheringWorldFixture
{
    internal static EntityId NodeId => Id(500);
    internal static EntityId NodeSourceId => Id(501);
    internal static EntityId ToolLotId => Id(505);

    internal static WorldSnapshot Create(uint effort = 1000, ulong storageCapacity = 64)
    {
        WorldSnapshot old = Genesis();
        var version = ContentHash.Compute("gathering-service-profile-v2"u8);
        CountedItemDefinition wood = old.Inventory.Sources.Single().Definition;
        var toolDefinition = new CountedItemDefinition(Id(503), version);
        var tool = new GatherToolDefinition(toolDefinition, GatherToolKind.Axe, 1);
        var node = new GatherNodeState(NodeId, new GatherNodeDefinition(Id(502), GatherNodeKind.Oak, wood),
            NodeSourceId, 20, new SubunitPosition(256, 0, 256), 0, 0, 5);
        var custody = new CustodySnapshot(old.WorldId, old.BranchId, old.Inventory.Custody.Revision,
            [new(Id(60), storageCapacity, 0), new(Id(61), 64, 0)],
            [.. old.Inventory.Custody.Lots, new CountedSourceLot(ToolLotId, Id(60), toolDefinition,
                new SourceInterval(Id(504), 0, 1), 0)]);
        var inventory = new CountedAuthorityState(custody, 27, old.Inventory.Actors, old.Inventory.Controls,
            old.Inventory.Policies, [.. old.Inventory.Sources, new(NodeSourceId, wood, 20), new(Id(504), toolDefinition, 1)]);
        var gathering = new GatheringSnapshot(old.WorldId, old.BranchId, 0, [node], [tool],
            old.Residents.Select(resident => new GatherActorWork(resident.ActorId, effort)));
        return new WorldSnapshot(inventory, old.Space, old.Residents, version, old.Calendar, 0, 0,
            gathering: gathering);
    }

    internal static GatherNodeState Node(WorldSnapshot state) => state.Gathering!.Nodes.Single();

    internal static WorldActionCommand Swing(WorldSnapshot state, ulong ordinal = 0) => new(Id(10_000 + ordinal),
        Delivery.PrincipalId, Delivery.SessionId, Id(10), state.WorldId, state.BranchId,
        state.Space.GetActor(Id(10)).Revision,
        new GatherIntent(NodeId, Node(state).Revision, ToolLotId, state.Inventory.Custody.GetLot(ToolLotId).Revision));

    internal static WorldStepBatch Batch(WorldSnapshot state, params WorldActionCommand[] actions) => new(
        Id(20_000 + state.Revision), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
        state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep, [], actions,
        state.ProfileVersion);
}
