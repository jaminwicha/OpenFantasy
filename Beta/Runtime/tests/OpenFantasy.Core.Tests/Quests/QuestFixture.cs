using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Quests;

internal sealed record QuestFixture(WorldSnapshot World, ProgressionSnapshot Progression, QuestSnapshot Quests)
{
    internal static readonly EntityId Project = Id(2000);
    internal static readonly EntityId Reservation = Id(2001);
    internal static readonly EntityId Reserve = Id(2206);
    internal DeliveryQuestDefinition Definition => Quests.Definitions.Single();

    internal WorldSnapshot AsWorld() => new(World.Inventory, World.Space, World.Residents, World.AdmissionVersion,
        World.Calendar, World.Revision, World.AdvancingSteps, World.Dialogues, World.Gathering, World.Production,
        Progression, Quests);

    internal static QuestFixture Create(bool dialogue = true, ulong funds = 200, ulong projectCapacity = 6)
    {
        WorldSnapshot basis = ProductionWorldFixture.Create();
        var inventory = new CountedAuthorityState(new CustodySnapshot(basis.WorldId, basis.BranchId,
            basis.Inventory.Custody.Revision, [.. basis.Inventory.Custody.Storages, new(Project, projectCapacity, 0)],
            basis.Inventory.Custody.Lots), basis.Inventory.AdmittedUnitCapacity, basis.Inventory.Actors,
            basis.Inventory.Controls, [.. basis.Inventory.Policies, new(Project, Id(11), CustodyDepositRule.AnyControlledActor)],
            basis.Inventory.Sources, [Reservation]);
        var world = new WorldSnapshot(inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, basis.Revision, basis.AdvancingSteps,
            dialogue ? [new DialogueSession(Id(2300), Id(10), Id(11), Id(92), Id(2301), basis.Calendar)] : [],
            basis.Gathering, basis.Production);
        var squire = new ActorStatProfileDefinition(Id(2200), "human_squire", "Human Squire", ActorStatProfileKind.Class,
            new ActorStatValues(60, 10, 12, 4, 8, 8), new ActorStatModifiers(9, 2, 3, 1, 2, 1), ActorStatModifiers.Zero);
        var unassigned = new ActorStatProfileDefinition(Id(2201), "unassigned", "Unassigned", ActorStatProfileKind.Unassigned,
            new ActorStatValues(1, 0, 0, 0, 0, 1), ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var curve = new LevelProgressionDefinition(Id(2202), "legacy_levels", [100, 282, 519, 800, 1118, 1469, 1852, 2262, 2700]);
        var gil = new CurrencyDefinition(Id(2203), "gil", "Gil", "gil");
        var currency = new CurrencyLedger(world.WorldId, world.BranchId, 0, gil, funds,
            [new(Id(2204), CurrencyAccountKind.Actor, Id(10), 0, 0),
             new(Id(2205), CurrencyAccountKind.Actor, Id(11), 0, 0),
             new(Reserve, CurrencyAccountKind.Reserve, null, funds, 0)]);
        var progression = new ProgressionSnapshot(world.WorldId, world.BranchId, 0, [squire, unassigned], [curve],
            [new(Id(10), 0, squire, curve, 1, 0, 7, 2), new(Id(11), 0, unassigned, curve, 1, 0, 1, 0)], currency);
        var reward = new ActorRewardDefinition(Id(2210), "wood_reward", 420, gil, Reserve, 200);
        var quest = new DeliveryQuestDefinition(Id(2211), "wood_delivery", "Orchard supplies", "Bring six wood.",
            Id(11), 1, Material, 6, Project, Reservation, reward);
        var quests = new QuestSnapshot(world.WorldId, world.BranchId, 0, [quest], []);
        quests.ValidateForAuthority(inventory, progression);
        return new QuestFixture(world, progression, quests);
    }

    internal WorldActionCommand Accept(ulong command = 2400, ulong actor = 10) => StepFixture.Action(World, command,
        new AcceptDeliveryQuestIntent(Definition.DefinitionId, Definition.Version, Definition.GiverActorId,
            World.Space.GetActor(Definition.GiverActorId).Revision,
            Quests.FindParticipation(Id(actor), Definition.DefinitionId)?.Revision ?? 0,
            Progression.GetActor(Id(actor)).Revision), actor);

    internal WorldActionCommand Complete(ulong command = 2401, IEnumerable<SourceLotSelection>? selections = null,
        ulong actor = 10) => StepFixture.Action(World, command,
        new CompleteDeliveryQuestIntent(Definition.DefinitionId, Definition.Version, Definition.GiverActorId,
            World.Space.GetActor(Definition.GiverActorId).Revision,
            Quests.FindParticipation(Id(actor), Definition.DefinitionId)?.Revision ?? 0,
            Progression.GetActor(Id(actor)).Revision,
            World.Inventory.Custody.GetStorage(World.GetResident(Id(actor)).InventoryCustodianId).Revision,
            selections ?? [new(Id(70), World.Inventory.Custody.GetLot(Id(70)).Revision, 6)]), actor);

    internal DeliveryQuestPlan Plan(WorldActionCommand command, Func<EntityId, bool>? occupied = null) =>
        new DeliveryQuestPlanner().Plan(World, World.Inventory, World.Production!, Progression, Quests,
            command, World.Dialogues, occupied);

    internal QuestFixture Apply(DeliveryQuestPlan plan) => new(new WorldSnapshot(plan.CandidateInventory,
        World.Space, World.Residents, World.AdmissionVersion, World.Calendar, World.Revision + 1, World.AdvancingSteps,
        World.Dialogues, World.Gathering, World.Production), plan.CandidateProgression, plan.CandidateQuests);
}
