using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Tests.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Gathering;

internal static class GatheringFixture
{
    internal static readonly CountedItemDefinition Axe = new(Id(201), ContentHash.Compute("gather-test-axe"u8));
    internal static readonly GatherNodeDefinition Oak = new(Id(202), GatherNodeKind.Oak, Material);
    internal static readonly EntityId Node = Id(203);
    internal static readonly EntityId Source = Id(204);
    internal static readonly EntityId ToolLot = Id(205);
    internal static readonly EntityId ToolSource = Id(206);

    internal static WorldSnapshot Create(uint effort = 1000, uint power = 1, ulong sourceCapacity = 20,
        ulong cursor = 0, int swings = 5, ulong step = 0, ulong? depletedAt = null,
        SubunitPosition? feet = null, ulong storageCapacity = 64, ulong nodeRevision = 0,
        LotProtection protection = LotProtection.None, EntityId? toolOwner = null,
        Func<LocalSpatialSnapshot, LocalSpatialSnapshot>? changeSpace = null)
    {
        WorldSnapshot initial = StepFixture.Create();
        var inventory = new CountedAuthorityState(new CustodySnapshot(initial.WorldId, initial.BranchId, 0,
            [new(Id(60), storageCapacity, 0), new(Id(61), storageCapacity, 0)],
            [.. initial.Inventory.Custody.Lots,
                new(ToolLot, toolOwner ?? Id(60), Axe, new SourceInterval(ToolSource, 0, 1), 0, protection)]),
            checked(7 + sourceCapacity), initial.Inventory.Actors, initial.Inventory.Controls, initial.Inventory.Policies,
            [.. initial.Inventory.Sources, new(ToolSource, Axe, 1), new(Source, Material, sourceCapacity)]);
        var gathering = new GatheringSnapshot(initial.WorldId, initial.BranchId, step,
            [new(Node, Oak, Source, sourceCapacity, feet ?? new SubunitPosition(256, 0, 256),
                nodeRevision, cursor, swings, depletedAt)], [new(Axe, GatherToolKind.Axe, power)],
            initial.Inventory.Actors.Select(actor => new GatherActorWork(actor, effort)));
        return new WorldSnapshot(inventory, changeSpace?.Invoke(initial.Space) ?? initial.Space, initial.Residents,
            initial.AdmissionVersion, initial.Calendar, step, step, gathering: gathering);
    }

    internal static WorldActionCommand Command(WorldSnapshot world, ulong identity = 300, ulong actor = 10,
        GatherIntent? intent = null) => StepFixture.Action(world, identity,
            intent ?? new GatherIntent(Node, world.Gathering!.GetNode(Node).Revision, ToolLot, 0), actor);

    internal static GatherPlan Plan(WorldSnapshot world, WorldActionCommand? command = null) =>
        new GatherPlanner().Plan(world, world.Inventory, world.Gathering!, command ?? Command(world));

    internal static WorldSnapshot With(WorldSnapshot world, CountedAuthorityState? inventory = null,
        GatheringSnapshot? gathering = null, LocalSpatialSnapshot? space = null,
        IEnumerable<DialogueSession>? dialogues = null) => new(inventory ?? world.Inventory, space ?? world.Space,
            world.Residents, world.AdmissionVersion, world.Calendar, world.Revision,
            gathering?.AdvancingStep ?? world.AdvancingSteps, dialogues ?? world.Dialogues, gathering ?? world.Gathering);
}
