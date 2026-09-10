using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class LocalQuestProjectionTests
{
    [Fact]
    public void AnOfferRequiresAddressedConversationAndRevealsNoReserveOrForeignWallet()
    {
        WorldSnapshot world = Create();
        Assert.Empty(new LocalWorldView(world, Id(10)).Quests);
        world = Apply(world, Action(world, 96_001, new SpeakToActorIntent(Id(11), 0)));
        LocalWorldView view = new(world, Id(10));
        LocalQuestView offer = Assert.Single(view.Quests);
        Assert.True(offer.CanAccept);
        Assert.False(offer.CanComplete);
        Assert.Equal(6UL, offer.EligibleQuantity);
        Assert.Equal(0UL, view.Progression!.CurrencyBalance);
        Assert.Equal((60, 10), (view.Progression.Hp, view.Progression.Mp));
        Assert.Empty(new LocalWorldView(world, Id(11)).Quests);
        string exposed = JsonSerializer.Serialize(view);
        Assert.DoesNotContain(QuestUpgradeFixture.Reserve.ToString(), exposed);
        Assert.DoesNotContain(QuestUpgradeFixture.Project.ToString(), exposed);
        Assert.DoesNotContain(QuestUpgradeFixture.Reservation.ToString(), exposed);
    }

    [Fact]
    public void OwnCommitmentSurvivesClosingDialogueAndShowsActualPaidResources()
    {
        WorldSnapshot world = Create();
        world = Apply(world, Action(world, 96_001, new SpeakToActorIntent(Id(11), 0)));
        world = Apply(world, QuestUpgradeFixture.Accept(world));
        LocalQuestView accepted = Assert.Single(new LocalWorldView(world, Id(10)).Quests);
        Assert.Equal(ActorQuestStatus.Accepted, accepted.Status);
        Assert.True(accepted.CanComplete);
        world = Apply(world, QuestUpgradeFixture.Complete(world));
        world = Apply(world, Action(world, 96_002, new CloseDialogueIntent(world.Dialogues.Single().DialogueId)));
        LocalWorldView view = new(world, Id(10));
        Assert.Null(view.Dialogue);
        LocalQuestView completed = Assert.Single(view.Quests);
        Assert.Equal(ActorQuestStatus.Completed, completed.Status);
        Assert.False(completed.CanAccept);
        Assert.False(completed.CanComplete);
        Assert.Equal((3, 38UL, 78, 14, 200UL), (view.Progression!.Level, view.Progression.Experience,
            view.Progression.Hp, view.Progression.Mp, view.Progression.CurrencyBalance));
        Assert.Equal(1UL, view.PlayerInventory.Quantity);
        Assert.Empty(new LocalWorldView(world, Id(11)).Quests);
    }

    [Fact]
    public void ProtectedWoodIsOwnedButNeverPresentedAsDeliverable()
    {
        WorldSnapshot world = Create();
        world = Apply(world, Action(world, 96_001, new SpeakToActorIntent(Id(11), 0)));
        world = Apply(world, QuestUpgradeFixture.Accept(world));
        CountedAuthorityState current = world.Inventory;
        CountedSourceLot wood = current.Custody.GetLot(Id(70));
        var custody = new CustodySnapshot(world.WorldId, world.BranchId, current.Custody.Revision,
            current.Custody.Storages, current.Custody.Lots.Select(lot => lot.LotId == wood.LotId
                ? new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, lot.Revision, LotProtection.Favorite) : lot));
        var inventory = new CountedAuthorityState(custody, current.AdmittedUnitCapacity, current.Actors, current.Controls,
            current.Policies, current.Sources, current.ReservationIds);
        world = new WorldSnapshot(inventory, world.Space, world.Residents, world.AdmissionVersion, world.Calendar,
            world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests);
        LocalWorldView view = new(world, Id(10));
        Assert.Equal(6UL, view.PlayerInventory.Lots.Single(lot => lot.LotId == Id(70)).Quantity);
        Assert.False(view.PlayerInventory.Lots.Single(lot => lot.LotId == Id(70)).AvailableForDelivery);
        Assert.Equal(0UL, view.Quests.Single().EligibleQuantity);
        Assert.False(view.Quests.Single().CanComplete);
    }

    private static WorldSnapshot Create()
    {
        WorldSnapshot gathered = GatheringWorldFixture.Create();
        WorldSnapshot production = new WorldProductionMigrationProcessor().Decide(gathered,
            ProductionUpgradeFixture.Request(gathered)).Candidate;
        return new WorldQuestMigrationProcessor().Decide(production, QuestUpgradeFixture.Request(production)).Candidate;
    }

    private static WorldSnapshot Apply(WorldSnapshot world, WorldActionCommand action) =>
        new WorldStepProcessor().Decide(world, GatheringWorldFixture.Batch(world, action)).Candidate;

    private static WorldActionCommand Action(WorldSnapshot world, ulong commandId, WorldActionIntent intent) =>
        new(Id(commandId), Delivery.PrincipalId, Delivery.SessionId, Id(10), world.WorldId, world.BranchId,
            world.Space.GetActor(Id(10)).Revision, intent);
}
