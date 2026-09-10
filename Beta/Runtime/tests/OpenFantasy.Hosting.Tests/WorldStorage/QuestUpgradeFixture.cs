using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal static class QuestUpgradeFixture
{
    internal static readonly EntityId Project = Id(95_010);
    internal static readonly EntityId Reservation = Id(95_011);
    internal static readonly EntityId Reserve = Id(95_012);

    internal static WorldQuestAdmission Admission(WorldSnapshot world, ulong funds = 200)
    {
        var profile = new ActorStatProfileDefinition(Id(95_020), "human_squire", "Human Squire",
            ActorStatProfileKind.Class, new(60, 10, 12, 4, 8, 8), new(9, 2, 3, 1, 2, 1), ActorStatModifiers.Zero);
        var unassigned = new ActorStatProfileDefinition(Id(95_021), "unassigned", "Unassigned",
            ActorStatProfileKind.Unassigned, new(1, 0, 0, 0, 0, 1), ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var curve = new LevelProgressionDefinition(Id(95_022), "levels", [100, 282, 519]);
        var gil = new CurrencyDefinition(Id(95_023), "gil", "Gil", "gil");
        var currency = new CurrencyLedger(world.WorldId, world.BranchId, 0, gil, funds,
            [new(Id(95_024), CurrencyAccountKind.Actor, Id(10), 0, 0),
             new(Id(95_025), CurrencyAccountKind.Actor, Id(11), 0, 0),
             new(Reserve, CurrencyAccountKind.Reserve, null, funds, 0)]);
        var progression = new ProgressionSnapshot(world.WorldId, world.BranchId, 0, [profile, unassigned], [curve],
            [new(Id(10), 0, profile, curve, 1, 0, 60, 10), new(Id(11), 0, unassigned, curve, 1, 0, 1, 0)], currency);
        CountedItemDefinition wood = world.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition;
        var reward = new ActorRewardDefinition(Id(95_026), "wood_reward", 420, gil, Reserve, 200);
        var quest = new DeliveryQuestDefinition(Id(95_027), "wood_delivery", "Orchard supplies", "Bring six wood.",
            Id(11), 1, wood, 6, Project, Reservation, reward);
        var quests = new QuestSnapshot(world.WorldId, world.BranchId, 0, [quest], []);
        return new WorldQuestAdmission(ContentHash.Compute("quest-upgrade-test-content"u8), progression, quests,
            [new(Project, Id(11), 6, Reservation, CustodyDepositRule.AnyControlledActor)]);
    }

    internal static WorldQuestMigration Request(WorldSnapshot world, EntityId? cause = null, ulong funds = 200)
    {
        WorldQuestAdmission admission = Admission(world, funds);
        return new(cause ?? Id(95_000), world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
            world.ContentHash, world.AdmissionVersion,
            WorldQuestMigration.ComputeTargetAdmissionVersion(world.AdmissionVersion, admission), admission);
    }

    internal static WorldActionCommand Accept(WorldSnapshot world, ulong command = 95_100)
    {
        DeliveryQuestDefinition quest = world.Quests!.Definitions.Single();
        return Action(world, command, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version,
            quest.GiverActorId, world.Space.GetActor(quest.GiverActorId).Revision,
            world.Quests.FindParticipation(Id(10), quest.DefinitionId)?.Revision ?? 0,
            world.Progression!.GetActor(Id(10)).Revision));
    }

    internal static WorldActionCommand Complete(WorldSnapshot world, ulong command = 95_101)
    {
        DeliveryQuestDefinition quest = world.Quests!.Definitions.Single();
        ulong remaining = quest.Quantity;
        List<SourceLotSelection> selections = [];
        foreach (CountedSourceLot lot in world.Inventory.Custody.Lots.Where(lot => lot.CustodianId == Id(60)
            && lot.Definition == quest.Item && lot.Protection == LotProtection.None && lot.ReservedBy is null))
        {
            ulong amount = Math.Min(remaining, lot.Quantity);
            if (amount == 0)
            {
                break;
            }
            selections.Add(new(lot.LotId, lot.Revision, amount));
            remaining -= amount;
        }
        Assert.Equal(0UL, remaining);
        return Action(world, command, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version,
            quest.GiverActorId, world.Space.GetActor(quest.GiverActorId).Revision,
            world.Quests.FindParticipation(Id(10), quest.DefinitionId)?.Revision ?? 0,
            world.Progression!.GetActor(Id(10)).Revision, world.Inventory.Custody.GetStorage(Id(60)).Revision, selections));
    }

    internal static QuestUpgradeHistory CreateCraftedHistory(string gatheringPath, string productionPath)
    {
        WorldSnapshot world = GatheringWorldFixture.Create();
        List<WorldActionCommand> actions = [];
        List<byte[]> receipts = [];
        using (SqliteWorldAuthority gathering = SqliteWorldAuthority.CreateNew(gatheringPath, world))
        {
            Commit(gathering, GatheringWorldFixture.Swing(world, 0));
            Commit(gathering, GatheringWorldFixture.Swing(world, 1));
        }
        using (SqliteWorldAuthority production = SqliteWorldAuthority.UpgradeGatheringToProduction(gatheringPath,
            productionPath, ProductionUpgradeFixture.Request(world)))
        {
            world = production.InspectForHost().State;
            Commit(production, ProductionUpgradeFixture.Craft(world));
            Commit(production, Action(world, 95_002,
                new SpeakToActorIntent(Id(11), world.Space.GetActor(Id(11)).Revision)));
        }
        return new(world, actions.AsReadOnly(), receipts.AsReadOnly());

        void Commit(SqliteWorldAuthority authority, WorldActionCommand action)
        {
            WorldStepDecision result = authority.Commit(GatheringWorldFixture.Batch(world, action));
            Assert.Equal(WorldActionRejection.None, result.Receipts.Single().Rejection);
            world = result.Candidate;
            actions.Add(action);
            receipts.Add(WorldReceiptCodecV1.Encode(result.Receipts.Single()));
        }
    }

    private static WorldActionCommand Action(WorldSnapshot world, ulong command, WorldActionIntent intent) =>
        new(Id(command), Delivery.PrincipalId, Delivery.SessionId, Id(10), world.WorldId, world.BranchId,
            world.Space.GetActor(Id(10)).Revision, intent);
}

internal sealed record QuestUpgradeHistory(WorldSnapshot State, IReadOnlyList<WorldActionCommand> Commands,
    IReadOnlyList<byte[]> Receipts);
